﻿using OpenNefia.Content.ConfigMenu;
using OpenNefia.Content.Logic;
using OpenNefia.Content.TurnOrder;
using OpenNefia.Content.UI.Layer;
using OpenNefia.Core;
using OpenNefia.Core.Audio;
using OpenNefia.Core.Configuration;
using OpenNefia.Core.Game;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Input;
using OpenNefia.Core.Input.Binding;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.SaveGames;
using OpenNefia.Core.UserInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenNefia.Content.Prototypes.Protos;

namespace OpenNefia.Content.GameObjects
{
    public class CommonCommandsSystem : EntitySystem
    {
        [Dependency] private readonly IPlayerQuery _playerQuery = default!;
        [Dependency] private readonly ITurnOrderSystem _turnOrderSystem = default!;
        [Dependency] private readonly IFieldLayer _field = default!;
        [Dependency] private readonly IAudioManager _sounds = default!;
        [Dependency] private readonly ISaveGameManager _saveGameManager = default!;
        [Dependency] private readonly ISaveGameSerializer _saveGameSerializer = default!;
        [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;
        [Dependency] private readonly IConfigurationManager _config = default!;

        public override void Initialize()
        {
            CommandBinds.Builder
                .Bind(EngineKeyFunctions.ShowEscapeMenu, InputCmdHandler.FromDelegate(ShowEscapeMenu))
                .Bind(EngineKeyFunctions.QuickSaveGame, InputCmdHandler.FromDelegate(QuickSaveGame))
                .Bind(EngineKeyFunctions.QuickLoadGame, InputCmdHandler.FromDelegate(QuickLoadGame))
                .Register<CommonCommandsSystem>();
        }

        private TurnResult? QuickSaveGame(IGameSessionManager? session)
        {
            var save = _saveGameManager.CurrentSave!;

            _saveGameSerializer.SaveGame(save);

            _sounds.Play(Sound.Write1);
            Mes.Display(Loc.GetString("Elona.UserInterface.Save.QuickSave"));

            return TurnResult.Aborted;
        }

        private TurnResult? QuickLoadGame(IGameSessionManager? session)
        {
            var save = _saveGameManager.CurrentSave!;

            _saveGameSerializer.LoadGame(save);

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
            if (!_turnOrderSystem.IsInGame())
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
                        ReturnToTitle();
                        break;
                    case EscapeMenuChoice.Exit:
                        ExitGame();
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
            ConfigMenuHelpers.ShowConfigMenu(_protos, _uiManager, _config);
        }

        private void ReturnToTitle()
        {
            Sounds.Play(Sound.Ok1);
            _field.Cancel();
        }

        private void ExitGame()
        {
            throw new NotImplementedException();
        }
    }
}
