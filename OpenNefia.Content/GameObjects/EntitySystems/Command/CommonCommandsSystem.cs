﻿using OpenNefia.Content.Actions;
using OpenNefia.Content.CharaInfo;
using OpenNefia.Content.ConfigMenu;
using OpenNefia.Content.GameController;
using OpenNefia.Content.Input;
using OpenNefia.Content.Journal;
using OpenNefia.Content.Logic;
using OpenNefia.Content.Maps;
using OpenNefia.Content.SaveLoad;
using OpenNefia.Content.Spells;
using OpenNefia.Content.UI.Layer;
using OpenNefia.Core;
using OpenNefia.Core.Audio;
using OpenNefia.Core.Configuration;
using OpenNefia.Core.ContentPack;
using OpenNefia.Core.Game;
using OpenNefia.Core.GameController;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Graphics;
using OpenNefia.Core.Input;
using OpenNefia.Core.Input.Binding;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.SaveGames;
using OpenNefia.Core.UI;
using OpenNefia.Core.UserInterface;
using OpenNefia.Core.Utility;
using static OpenNefia.Content.CharaInfo.CharaGroupSublayerArgs;
using static OpenNefia.Content.Journal.LogGroupSublayerArgs;
using static OpenNefia.Content.Prototypes.Protos;
using static OpenNefia.Content.Spells.SpellGroupSublayerArgs;

namespace OpenNefia.Content.GameObjects
{
    public interface ICommonCommandsSystem : IEntitySystem
    {
        bool BlockIfInWorldMap(EntityUid player);
        InputCmdHandler MakeCommandHandler(StateInputCmdDelegate enabled, bool blockInWorldMap = false);
        InputCmdHandler MakeCommandHandler(InputCmdHandler inner, bool blockInWorldMap = false);
    }

    public class CommonCommandsSystem : EntitySystem, ICommonCommandsSystem
    {
        [Dependency] private readonly IPlayerQuery _playerQuery = default!;
        [Dependency] private readonly IFieldLayer _field = default!;
        [Dependency] private readonly IAudioManager _sounds = default!;
        [Dependency] private readonly ISaveGameManager _saveGameManager = default!;
        [Dependency] private readonly ISaveGameSerializer _saveGameSerializer = default!;
        [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;
        [Dependency] private readonly IConfigurationManager _config = default!;
        [Dependency] private readonly IGameController _gameController = default!;
        [Dependency] private readonly IGraphics _graphics = default!;
        [Dependency] private readonly IMessagesManager _mes = default!;
        [Dependency] private readonly ISaveLoadSystem _saveLoad = default!;
        [Dependency] private readonly ISpellSystem _spells = default!;
        [Dependency] private readonly IActionSystem _actions = default!;

        public bool BlockIfInWorldMap(EntityUid player)
        {
            if (TryMap(player, out var map) && HasComp<MapTypeWorldMapComponent>(map.MapEntityUid))
            {
                _mes.Display(Loc.GetString("Elona.Common.CannotDoInGlobal", ("entity", player)));
                return true;
            }

            return false;
        }
        /// <summary>
        /// Optionally wraps an input command delegate with a check if the player is in the world map.
        /// </summary>
        public InputCmdHandler MakeCommandHandler(StateInputCmdDelegate enabled, bool blockInWorldMap = false)
        {
            if (blockInWorldMap)
            {
                var oldEnabled = enabled;
                enabled = (IGameSessionManager? session) =>
                {
                    if (BlockIfInWorldMap(session!.Player))
                        return TurnResult.Aborted;

                    return oldEnabled(session);
                };
            }

            return InputCmdHandler.FromDelegate(enabled);
        }

        /// <summary>
        /// Optionally wraps an input command delegate with a check if the player is in the world map.
        /// </summary>
        public InputCmdHandler MakeCommandHandler(InputCmdHandler inner, bool blockInWorldMap = false)
        {
            return MakeCommandHandler(inner.Enabled, blockInWorldMap);
        }

        public override void Initialize()
        {
            CommandBinds.Builder
                // CharaInfo group
                .Bind(ContentKeyFunctions.CharaInfo, MakeCommandHandler(ShowCharaInfo))
                .Bind(ContentKeyFunctions.Equipment, MakeCommandHandler(ShowEquipment))
                .Bind(ContentKeyFunctions.FeatInfo, MakeCommandHandler(ShowFeatInfo))

                // Journal group
                .Bind(ContentKeyFunctions.Backlog, MakeCommandHandler(ShowBacklog))
                .Bind(ContentKeyFunctions.Journal, MakeCommandHandler(ShowJournal))
                .Bind(ContentKeyFunctions.ChatLog, MakeCommandHandler(ShowChatLog))

                // Spell group
                .Bind(ContentKeyFunctions.Cast, MakeCommandHandler(ShowSpells, blockInWorldMap: true))
                .Bind(ContentKeyFunctions.Skill, MakeCommandHandler(ShowActions, blockInWorldMap: true))

                // Other commands
                .Bind(EngineKeyFunctions.ShowEscapeMenu, MakeCommandHandler(ShowEscapeMenu))
                .Bind(EngineKeyFunctions.QuickSaveGame, MakeCommandHandler(QuickSaveGame))
                .Bind(EngineKeyFunctions.QuickLoadGame, MakeCommandHandler(QuickLoadGame))
                .Register<CommonCommandsSystem>();
        }

        private TurnResult? ShowCharaInfo(IGameSessionManager? session)
        {
            if (session?.Player == null)
                return null;

            var context = new CharaUiGroupArgs(CharaTab.CharaInfo, session.Player);
            var result = _uiManager.Query<CharaUiGroup, CharaUiGroupArgs, CharaGroupSublayerResult>(context);

            return HandleCharaUiGroupResult(result);
        }

        private TurnResult? ShowEquipment(IGameSessionManager? session)
        {
            if (session?.Player == null)
                return null;

            var context = new CharaUiGroupArgs(CharaTab.Equipment, session.Player);
            var result = _uiManager.Query<CharaUiGroup, CharaUiGroupArgs, CharaGroupSublayerResult>(context);

            return HandleCharaUiGroupResult(result);
        }

        private TurnResult? ShowFeatInfo(IGameSessionManager? session)
        {
            if (session?.Player == null)
                return null;

            var context = new CharaUiGroupArgs(CharaTab.FeatInfo, session.Player);
            var result = _uiManager.Query<CharaUiGroup, CharaUiGroupArgs, CharaGroupSublayerResult>(context);

            return HandleCharaUiGroupResult(result);
        }

        private TurnResult HandleCharaUiGroupResult(UiResult<CharaGroupSublayerResult> result)
        {
            if (result.HasValue && result.Value.ChangedEquipment)
            {
                _mes.Display(Loc.GetString("Elona.Equipment.YouChangeYourEquipment"));
                return TurnResult.Succeeded;
            }

            return TurnResult.Aborted;
        }

        private TurnResult? ShowJournal(IGameSessionManager? session)
        {
            var context = new LogUiGroupArgs(LogTab.Journal);
            _uiManager.Query<LogUiGroup, LogUiGroupArgs>(context);

            return TurnResult.Aborted;
        }

        private TurnResult? ShowBacklog(IGameSessionManager? session)
        {
            var context = new LogUiGroupArgs(LogTab.Backlog);
            _uiManager.Query<LogUiGroup, LogUiGroupArgs>(context);

            return TurnResult.Aborted;
        }

        private TurnResult? ShowChatLog(IGameSessionManager? session)
        {
            var context = new LogUiGroupArgs(LogTab.ChatLog);
            _uiManager.Query<LogUiGroup, LogUiGroupArgs>(context);

            return TurnResult.Aborted;
        }

        private TurnResult? ShowSpells(IGameSessionManager? session)
        {
            var context = new SpellUiGroupArgs(SpellTab.Spell, session!.Player);
            var result = _uiManager.Query<SpellUiGroup, SpellUiGroupArgs, SpellGroupSublayerResult>(context);

            if (!result.HasValue)
                return TurnResult.Aborted;

            return HandleSkillOrSpellResult(session!.Player, result.Value);
        }

        private TurnResult? ShowActions(IGameSessionManager? session)
        {
            var context = new SpellUiGroupArgs(SpellTab.Skill, session!.Player);
            var result = _uiManager.Query<SpellUiGroup, SpellUiGroupArgs, SpellGroupSublayerResult>(context);

            if (!result.HasValue)
                return TurnResult.Aborted;

            return HandleSkillOrSpellResult(session!.Player, result.Value);
        }

        private TurnResult? HandleSkillOrSpellResult(EntityUid player, SpellGroupSublayerResult value)
        {
            // TODO block in world map.

            switch (value)
            {
                case SpellGroupSublayerResult.CastSpell castSpell:
                    return _spells.Cast(player, castSpell.Spell.GetStrongID());
                case SpellGroupSublayerResult.InvokeAction invokeAction:
                    return _actions.InvokeAction(player, invokeAction.Action.GetStrongID());
                default:
                    return TurnResult.Aborted;
            }
        }

        private TurnResult? QuickSaveGame(IGameSessionManager? session)
        {
            _saveLoad.QuickSaveGame();

            return TurnResult.Aborted;
        }

        private TurnResult? QuickLoadGame(IGameSessionManager? session)
        {
            _saveLoad.QuickLoadGame();

            return TurnResult.Aborted;
        }

        private enum EscapeMenuChoice
        {
            Cancel,
            GameSetting,
            ReturnToTitle,
            Exit,
        }

        private TurnResult? ShowEscapeMenu(IGameSessionManager? session)
        {
            if (!_field.IsInGame() || session?.Player == null)
                return null;

            var keyRoot = new LocaleKey("Elona.UserInterface.Exit.Prompt.Choices");
            var choices = new PromptChoice<EscapeMenuChoice>[]
            {
#pragma warning disable format
                new(EscapeMenuChoice.Cancel,        keyRoot.With(nameof(EscapeMenuChoice.Cancel))),
                new(EscapeMenuChoice.GameSetting,   keyRoot.With(nameof(EscapeMenuChoice.GameSetting))),
                new(EscapeMenuChoice.ReturnToTitle, keyRoot.With(nameof(EscapeMenuChoice.ReturnToTitle))),
                new(EscapeMenuChoice.Exit,          keyRoot.With(nameof(EscapeMenuChoice.Exit)))
#pragma warning restore format
            };

            var promptArgs = new Prompt<EscapeMenuChoice>.Args(choices)
            {
                QueryText = Loc.GetString("Elona.UserInterface.Exit.Prompt.Text")
            };

            var result = _uiManager.Query<Prompt<EscapeMenuChoice>,
                Prompt<EscapeMenuChoice>.Args,
                PromptChoice<EscapeMenuChoice>>(promptArgs);

            if (result.HasValue)
            {
                switch (result.Value.ChoiceData)
                {
                    case EscapeMenuChoice.GameSetting:
                        ShowConfigMenu();
                        break;
                    case EscapeMenuChoice.ReturnToTitle:
                        ReturnToTitle(session!);
                        break;
                    case EscapeMenuChoice.Exit:
                        ExitGame(session!);
                        break;
                    case EscapeMenuChoice.Cancel:
                    default:
                        break;
                }
            }

            return TurnResult.Aborted;
        }

        private void ShowConfigMenu()
        {
            Sounds.Play(Sound.Ok1);
            ConfigMenuHelpers.QueryDefaultConfigMenu(_protos, _uiManager, _config);
        }

        private void ReturnToTitle(IGameSessionManager gameSession)
        {
            bool save;

            switch (_config.GetCVar(CCVars.GameSaveOnReturnToTitle))
            {
                case SaveOnReturnToTitle.Always:
                default:
                    save = true;
                    break;
                case SaveOnReturnToTitle.Ask:
                    var response = _playerQuery.YesOrNoOrCancel(Loc.GetString("Elona.UserInterface.Exit.Prompt.PromptSaveGame"));
                    if (response == null)
                        return;
                    save = response.Value;
                    break;
                case SaveOnReturnToTitle.Never:
                    save = false;
                    break;
            }

            if (save)
            {
                QuickSaveGame(gameSession);
                _mes.Display(Loc.GetString("Elona.UserInterface.Exit.Saved"));
                _playerQuery.PromptMore();
                _gameController.WaitSecs(0.3f);
            }

            _field.Cancel();
        }

        private void ExitGame(IGameSessionManager gameSession)
        {
            QuickSaveGame(gameSession);
            _mes.Display(Loc.GetString("Elona.UserInterface.Exit.Saved"));
            _mes.Display(Loc.GetString("Elona.UserInterface.Exit.YouCloseYourEyes", ("entity", gameSession.Player!)));
            _playerQuery.PromptMore();
            _gameController.WaitSecs(0.3f);

            _gameController.Shutdown();
        }
    }
}
