﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SM64_Diagnostic.Structs;
using System.Windows.Forms;
using SM64_Diagnostic.Utilities;
using SM64_Diagnostic.Extensions;
using SM64_Diagnostic.Controls;
using SM64_Diagnostic.Structs.Configurations;

namespace SM64_Diagnostic.Managers
{
    public class MiscManager : DataManager
    {
        BetterTextbox _betterTextboxRNGIndex;
        CheckBox _checkBoxTurnOffMusic;

        public MiscManager(List<VarXControl> variables, NoTearFlowLayoutPanel variableTable, Control miscControl)
            : base(null, variableTable, true, variables)
        {
            SplitContainer splitContainerMisc = miscControl.Controls["splitContainerMisc"] as SplitContainer;
            GroupBox groupBoxRNGIndex = splitContainerMisc.Panel1.Controls["groupBoxRNGIndex"] as GroupBox;
            _betterTextboxRNGIndex = groupBoxRNGIndex.Controls["betterTextboxRNGIndex"] as BetterTextbox;
            _betterTextboxRNGIndex.AddEnterAction(() =>
            {
                int? index = ParsingUtilities.ParseIntNullable(_betterTextboxRNGIndex.Text);
                if (index.HasValue)
                {
                    ushort rngValue = RngIndexer.GetRngValue(index.Value);
                    Config.Stream.SetValue(rngValue, Config.RngAddress);
                }
            });

            _checkBoxTurnOffMusic = splitContainerMisc.Panel1.Controls["checkBoxTurnOffMusic"] as CheckBox;
        }

        public override void Update(bool updateView)
        {
            if (_checkBoxTurnOffMusic.Checked)
            {
                byte oldMusicByte = Config.Stream.GetByte(Config.MusicOnAddress);
                byte newMusicByte = MoreMath.ApplyValueToMaskedByte(oldMusicByte, Config.MusicOnMask, true);
                Config.Stream.SetValue(newMusicByte, Config.MusicOnAddress);
                Config.Stream.SetValue(0f, Config.MusicVolumeAddress);
            }

            if (!updateView)
                return;

            base.Update();
        }

    }
}
