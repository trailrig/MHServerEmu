﻿using System.Text;
using MHServerEmu.Core.Serialization;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.GameData;

namespace MHServerEmu.Games.UI.Widgets
{
    public class UIWidgetButton : UISyncData
    {
        // NOTE: This widget may be unfinished. The only place it seems to may have been used is
        // MetaStateShutdown for DangerRoom, but the TeleportButtonWidget field does not have
        // any data in any of the prototypes, and even the TeleportButtonWidget prototype itself
        // does not seem to be referenced anywhere else.
        //
        // It does sort of work, but the visuals for it get outside the bounds of the widget bar.

        private readonly List<CallbackBase> _callbackList = new();

        public UIWidgetButton(UIDataProvider uiDataProvider, PrototypeId widgetRef, PrototypeId contextRef) : base(uiDataProvider, widgetRef, contextRef) { }

        public override bool Serialize(Archive archive)
        {
            bool success = true;

            success &= base.Serialize(archive);

            uint numCallbacks = (uint)_callbackList.Count;
            success &= Serializer.Transfer(archive, ref numCallbacks);

            if (archive.IsPacking)
            {
                foreach (CallbackBase callback in _callbackList)
                {
                    ulong playerGuid = callback.PlayerGuid;
                    success &= Serializer.Transfer(archive, ref playerGuid);
                }
            }
            else
            {
                _callbackList.Clear();
                for (uint i = 0; i < numCallbacks; i++)
                {
                    ulong playerGuid = 0;
                    success &= Serializer.Transfer(archive, ref playerGuid);
                    CallbackBase callback = new(playerGuid);
                    _callbackList.Add(callback);
                }
            }

            return success;
        }

        protected override void BuildString(StringBuilder sb)
        {
            base.BuildString(sb);

            for (int i = 0; i < _callbackList.Count; i++)
                sb.AppendLine($"{nameof(_callbackList)}[{i}]: {_callbackList[i]}");
        }

        public void AddCallback(ulong playerGuid)
        {
            _callbackList.Add(new(playerGuid));
            UpdateUI();
        }

        class CallbackBase
        {
            public ulong PlayerGuid { get; }

            public CallbackBase(ulong playerGuid)
            {
                PlayerGuid = playerGuid;
            }

            public override string ToString()
            {
                return $"{nameof(PlayerGuid)}: 0x{PlayerGuid:X16}";
            }
        }
    }
}
