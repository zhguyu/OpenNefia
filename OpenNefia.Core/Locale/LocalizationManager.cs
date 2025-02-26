﻿using OpenNefia.Core.IoC;
using OpenNefia.Core.Utility;
using System.Reflection;
using NLua;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.ContentPack;
using System.Buffers;
using OpenNefia.Core.Random;
using OpenNefia.Core.Log;
using OpenNefia.Core.Reflection;
using OpenNefia.Core.Graphics;
using System.Diagnostics.CodeAnalysis;
using OpenNefia.Core.UserInterface;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Timing;
using OpenNefia.Core.Configuration;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Spectre.Console;

namespace OpenNefia.Core.Locale
{
    public interface ILocalizationFetcher
    {
        /// <summary>
        /// Returns true if this key exists in any form in the localization environment (string, list, function, etc.)
        /// </summary>
        bool KeyExists(LocaleKey key);
        bool PrototypeKeyExists<T>(PrototypeId<T> protoID, LocaleKey key) where T: class, IPrototype;
        bool PrototypeKeyExists<T>(T proto, LocaleKey key) where T: class, IPrototype;

        bool TryGetString(LocaleKey key, [NotNullWhen(true)] out string? str, params LocaleArg[] args);
        string GetString(LocaleKey key, params LocaleArg[] args);

        string GetPrototypeString<T>(PrototypeId<T> protoId, LocaleKey key, params LocaleArg[] args)
            where T: class, IPrototype;
        string GetPrototypeStringRaw(Type prototypeType, string prototypeID, LocaleKey keySuffix, LocaleArg[] args);
        bool TryGetPrototypeString<T>(PrototypeId<T> protoId, LocaleKey key, [NotNullWhen(true)] out string? str, params LocaleArg[] args)
            where T : class, IPrototype;
        bool TryGetPrototypeStringRaw(Type prototypeType, string prototypeID, LocaleKey keySuffix, [NotNullWhen(true)] out string? str, params LocaleArg[] args);

        bool TryGetList(LocaleKey key, [NotNullWhen(true)] out IReadOnlyList<string>? list, params LocaleArg[] args);
        IReadOnlyList<string> GetList(LocaleKey key, params LocaleArg[] args);
        bool TryGetPrototypeList<T>(PrototypeId<T> protoId, LocaleKey key, [NotNullWhen(true)] out IReadOnlyList<string>? list, params LocaleArg[] args)
    where T : class, IPrototype;
        bool TryGetPrototypeListRaw(Type prototypeType, string prototypeID, LocaleKey keySuffix, [NotNullWhen(true)] out IReadOnlyList<string>? list, params LocaleArg[] args);

        bool TryGetTable(LocaleKey key, [NotNullWhen(true)] out LuaTable? table);
        LuaTable GetTable(LocaleKey key);
        string FormatRaw(object? obj, LocaleArg[] args);
    }

    public delegate void LanguageSwitchedDelegate(PrototypeId<LanguagePrototype> newLanguage);

    public interface ILocalizationManager : ILocalizationFetcher
    {
        PrototypeId<LanguagePrototype> Language { get; }

        void Initialize();

        bool IsFullwidth();
        void SwitchLanguage(PrototypeId<LanguagePrototype> language);

        void DoLocalize(object o, LocaleKey key);

        void LoadContentFile(ResourcePath luaFile);
        void LoadString(string luaScript);
        void Resync();

        bool TryGetLocalizationData(EntityUid uid, [NotNullWhen(true)] out LuaTable? table);
        EntityLocData GetEntityData(string prototypeId);

        event LanguageSwitchedDelegate? OnLanguageSwitched;
    }

    /// <summary>
    /// An argument for localizing text using a Lua function.
    /// </summary>
    public struct LocaleArg
    {
        /// <summary>
        /// Name of this argument in the Lua source.
        /// </summary>
        /// <remarks>
        /// NOTE: This is purely for documentation purposes.
        /// </remarks>
        public string Name { get; set; }

        /// <summary>
        /// Value of this argument to pass to a Lua function.
        /// </summary>
        public object? Value { get; set; }

        public LocaleArg(string name, object? value)
        {
            Name = name;
            Value = value;
        }

        public static implicit operator LocaleArg((string, object?) tuple)
        {
            return new(tuple.Item1, tuple.Item2);
        }
    }

    public partial class LocalizationManager : ILocalizationManager
    {
        [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
        [Dependency] private readonly IResourceManager _resourceManager = default!;
        [Dependency] private readonly IReflectionManager _reflectionManager = default!;
        [Dependency] private readonly IGraphics _graphics = default!;
        [Dependency] private readonly IRandom _random = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IConfigurationManager _config = default!;
        [Dependency] private readonly IComponentLocalizer _componentLocalizer = default!;
        [Dependency] private readonly IPrototypeManagerInternal _protos = default!;

        public event LanguageSwitchedDelegate? OnLanguageSwitched;

        private readonly ResourcePath LocalePath = new ResourcePath("/Locale");

        private const string LocDataKey = "_LocData";
        private ConcurrentDictionary<string, EntityLocData> _entityCache = new();

        private readonly HashSet<LocaleKey> _missingKeys = new();

        public PrototypeId<LanguagePrototype> Language { get; private set; } = LanguagePrototypeOf.English;

        public void Initialize()
        {
            _lua = CreateLuaEnv();
            ScanBuiltInFunctions();

            _config.OnValueChanged(CVars.LanguageLanguage, OnConfigLanguageChanged, true);

            _graphics.OnWindowFocusChanged += WindowFocusedChanged;

            WatchResources();
        }

        private void OnConfigLanguageChanged(string rawID)
        {
            PrototypeId<LanguagePrototype> protoId = new(rawID);

            // This makes the test suite have a hard dependency on Core prototypes.
            /*
            if (!_protos.HasIndex(protoId))
            {
                protoId = LanguagePrototypeOf.English;
                Logger.WarningS("loc", $"No language with ID '{rawID}' registered; falling back to {protoId}");
            }
            */

            SwitchLanguage(protoId);
        }
        
        public void SwitchLanguage(PrototypeId<LanguagePrototype> language)
        {
            using var profiler = new ProfilerLogger(LogLevel.Info, "loc", $"Switching language to {language}");

            _missingKeys.Clear();

            Language = language;
            SetLanguage(language);
            LoadAll(language, LocalePath);
            Resync();

            foreach (var layer in _uiManager.ActiveLayers)
            {
                layer.Localize();
            }

            foreach (var uid in _entityManager.GetEntityUids())
            {
                _componentLocalizer.LocalizeComponents(uid);
            }
            _entityCache.Clear();

            OnLanguageSwitched?.Invoke(language);
        }

        /// <inheritdoc//>
        public bool KeyExists(LocaleKey key)
        {
            return (_stringStore.ContainsKey(key) || _functionStore.ContainsKey(key) || _listStore.ContainsKey(key));
        }

        /// <inheritdoc//>
        public bool PrototypeKeyExists<T>(PrototypeId<T> protoID, LocaleKey key) where T: class, IPrototype
            => KeyExists(GetPrototypeLocaleKey(protoID).With(key));

        /// <inheritdoc//>
        public bool PrototypeKeyExists<T>(T proto, LocaleKey key) where T : class, IPrototype
            => KeyExists(GetPrototypeLocaleKey(proto).With(key));

        private static string CallFunction(LocaleKey key, LocaleArg[] args, LuaFunction func)
        {
            string? str;
            var shared = ArrayPool<object?>.Shared;
            var rented = shared.Rent(args.Length);

            for (int i = 0; i < args.Length; i++)
                rented[i] = args[i].Value;

            try
            {
                var result = func.Call(rented).FirstOrDefault();
                str = $"{result ?? "null"}";
            }
            catch (Exception ex)
            {
                Logger.ErrorS("loc", ex, $"Error in locale function ({key}): {ex}");
                str = $"<exception: {ex.Message} ({key})>";
            }

            shared.Return(rented);
            return str;
        }

        public bool TryGetString(LocaleKey key, [NotNullWhen(true)] out string? str, params LocaleArg[] args)
        {

            if (_stringStore.TryGetValue(key, out str))
            {
                return true;
            }

            if (_functionStore.TryGetValue(key, out var func))
            {
                str = CallFunction(key, args, func);

                return true;
            }

            if (_listStore.TryGetValue(key, out var list))
            {
                // This is meant to emulate the `txt` function in the HSP source.
                var obj = _random.Pick(list);

                if (obj is LuaFunction func2)
                {
                    str = CallFunction(key, args, func2);
                }
                else
                {
                    str = obj.ToString()!;
                }

                return true;
            }

            return false;
        }

        public string GetString(LocaleKey key, params LocaleArg[] args)
        {
            if (TryGetString(key, out var str, args))
                return str;

            if (!_missingKeys.Contains(key))
            {
                Logger.WarningS("loc", $"Missing locale key: {key}");
                _missingKeys.Add(key);
            }

            return $"<Missing key: {key}>";
        }

        public string GetStringOrEmpty(LocaleKey key, params LocaleArg[] args)
        {
            if (TryGetString(key, out var str, args))
                return str;

            return string.Empty;
        }

        public bool TryGetTable(LocaleKey key, [NotNullWhen(true)] out LuaTable? table)
        {
            table = _lua.GetTable($"_Collected." + key);
            return table != null;
        }

        public LuaTable GetTable(LocaleKey key)
        {
            if (TryGetTable(key, out var table))
                return table;

            throw new KeyNotFoundException(key);
        }

        public string FormatRaw(object? obj, LocaleArg[] args)
        {
            if (obj is string s)
                return s;

            if (obj is LuaFunction func)
                return CallFunction("<???>", args, func);

            return $"{obj}";
        }

        public string GetPrototypeString<T>(PrototypeId<T> protoId, LocaleKey key, params LocaleArg[] args)
            where T : class, IPrototype
            => GetPrototypeStringRaw(typeof(T), (string)protoId, key, args);

        public static LocaleKey GetPrototypeLocaleKeyRaw(Type prototypeType, string prototypeID)
        {
            var protoTypeId = prototypeType.GetCustomAttribute<PrototypeAttribute>()!.Type;
            return new LocaleKey($"OpenNefia.Prototypes.{protoTypeId}.{prototypeID}");
        }

        public static LocaleKey GetPrototypeLocaleKey<T>(PrototypeId<T> prototypeID) where T: class, IPrototype
            => GetPrototypeLocaleKeyRaw(typeof(T), (string)prototypeID);

        public static LocaleKey GetPrototypeLocaleKey<T>(T prototype) where T : class, IPrototype
            => GetPrototypeLocaleKeyRaw(typeof(T), prototype.ID);

        public string GetPrototypeStringRaw(Type prototypeType, string prototypeID, LocaleKey keySuffix, LocaleArg[] args)
        {
            var key = GetPrototypeLocaleKeyRaw(prototypeType, prototypeID);
            return GetString(key.With(keySuffix), args);
        }

        public bool TryGetPrototypeString<T>(PrototypeId<T> protoId, LocaleKey key, [NotNullWhen(true)] out string? str, params LocaleArg[] args)
            where T : class, IPrototype
            => TryGetPrototypeStringRaw(typeof(T), (string)protoId, key, out str, args);

        public bool TryGetPrototypeStringRaw(Type prototypeType, string prototypeID, LocaleKey keySuffix, [NotNullWhen(true)] out string? str, params LocaleArg[] args)
        {
            var key = GetPrototypeLocaleKeyRaw(prototypeType, prototypeID);
            return TryGetString(key.With(keySuffix), out str, args);
        }

        public bool TryGetList(LocaleKey key, [NotNullWhen(true)] out IReadOnlyList<string>? list, params LocaleArg[] args)
        {
            if (_listStore.TryGetValue(key, out var foundList))
            {
                var result = new List<string>();
                foreach (var obj in foundList)
                {
                    if (obj is LuaFunction func2)
                    {
                        result.Add(CallFunction(key, args, func2));
                    }
                    else
                    {
                        result.Add(obj.ToString()!);
                    }
                }
                list = result;
                return true;
            }

            list = null;
            return false;
        }

        public IReadOnlyList<string> GetList(LocaleKey key, params LocaleArg[] args)
        {
            if (TryGetList(key, out var list, args))
                return list;

            if (!_missingKeys.Contains(key))
            {
                Logger.WarningS("loc", $"Missing locale key (list): {key}");
                _missingKeys.Add(key);
            }

            return new List<string>() { $"<Missing key: {key}>" };
        }

        public bool TryGetPrototypeList<T>(PrototypeId<T> protoId, LocaleKey key, [NotNullWhen(true)] out IReadOnlyList<string>? list, params LocaleArg[] args)
            where T : class, IPrototype
            => TryGetPrototypeListRaw(typeof(T), (string)protoId, key, out list, args);

        public bool TryGetPrototypeListRaw(Type prototypeType, string prototypeID, LocaleKey keySuffix, [NotNullWhen(true)] out IReadOnlyList<string>? list, params LocaleArg[] args)
        {
            var key = GetPrototypeLocaleKeyRaw(prototypeType, prototypeID);
            return TryGetList(key.With(keySuffix), out list, args);
        }

        public bool IsFullwidth()
        {
            return Language == LanguagePrototypeOf.Japanese;
        }

        public bool TryGetLocalizationData(EntityUid uid, [NotNullWhen(true)] out LuaTable? table)
        {
            if (!_entityManager.TryGetComponent(uid, out MetaDataComponent? metadata)
                || metadata.EntityPrototype == null)
            {
                table = null;
                return false;
            }

            var key = GetPrototypeLocaleKey(metadata.EntityPrototype);
            return TryGetTable(key, out table);
        }

        private EntityLocData ReadEntityLocDataFromLua(string id)
        {
            var key = GetPrototypeLocaleKeyRaw(typeof(EntityPrototype), id);
            if (!TryGetTable(key, out var entityTable)
                || !entityTable.TryGetTable(LocDataKey, out var locTable))
                return new(ImmutableDictionary.Create<string, string>());

            var builder = ImmutableDictionary.CreateBuilder<string, string>();
            foreach (KeyValuePair<object, object> locData in locTable)
                builder.Add(locData.Key.ToString()!, locData.Value.ToString()!);

            return new(builder.ToImmutable());
        }

        public EntityLocData GetEntityData(string prototypeId)
        {
            return _entityCache.GetOrAdd(prototypeId ?? string.Empty, (id, t) => t.ReadEntityLocDataFromLua(id), this);
        }
    }
}
