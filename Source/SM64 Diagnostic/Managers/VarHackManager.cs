﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SM64_Diagnostic.Structs;
using System.Windows.Forms;
using SM64_Diagnostic.Utilities;
using SM64_Diagnostic.Controls;
using SM64_Diagnostic.Extensions;
using SM64_Diagnostic.Structs.Configurations;
using static SM64_Diagnostic.Utilities.ControlUtilities;

namespace SM64_Diagnostic.Managers
{
    public class VarHackManager
    {
        public static VarHackManager Instance;

        private readonly VarHackPanel _varHackPanel;
        private readonly BinaryButton _buttonEnableDisableRomHack;

        private readonly TextBox _textBoxXPosValue;
        private readonly TextBox _textBoxYPosValue;
        private readonly TextBox _textBoxYDeltaValue;

        public VarHackManager(Control varHackControlControl, VarHackPanel varHackPanel)
        {
            Instance = this;

            _varHackPanel = varHackPanel;

            SplitContainer splitContainerVarHack =
                varHackControlControl.Controls["splitContainerVarHack"] as SplitContainer;

            Button buttonVarHackAddNewVariable =
                splitContainerVarHack.Panel1.Controls["buttonVarHackAddNewVariable"] as Button;
            buttonVarHackAddNewVariable.Click +=
                (sender, e) => _varHackPanel.AddNewControl();

            Button buttonVarHackClearVariables =
                splitContainerVarHack.Panel1.Controls["buttonVarHackClearVariables"] as Button;
            buttonVarHackClearVariables.Click +=
                (sender, e) => _varHackPanel.ClearControls();

            Button buttonVarHackShowVariableBytesInLittleEndian =
                splitContainerVarHack.Panel1.Controls["buttonVarHackShowVariableBytesInLittleEndian"] as Button;
            buttonVarHackShowVariableBytesInLittleEndian.Click +=
                (sender, e) => _varHackPanel.ShowVariableBytesInLittleEndian();

            Button buttonVarHackShowVariableBytesInBigEndian =
                splitContainerVarHack.Panel1.Controls["buttonVarHackShowVariableBytesInBigEndian"] as Button;
            buttonVarHackShowVariableBytesInBigEndian.Click +=
                (sender, e) => _varHackPanel.ShowVariableBytesInBigEndian();

            Button buttonVarHackApplyVariablesToMemory =
                splitContainerVarHack.Panel1.Controls["buttonVarHackApplyVariablesToMemory"] as Button;
            buttonVarHackApplyVariablesToMemory.Click +=
                (sender, e) => _varHackPanel.ApplyVariablesToMemory();

            Button buttonVarHackClearVariablesInMemory =
                splitContainerVarHack.Panel1.Controls["buttonVarHackClearVariablesInMemory"] as Button;
            buttonVarHackClearVariablesInMemory.Click +=
                (sender, e) => _varHackPanel.ClearVariablesInMemory();

            _buttonEnableDisableRomHack =
                splitContainerVarHack.Panel1.Controls["buttonEnableDisableRomHack"] as BinaryButton;
            _buttonEnableDisableRomHack.Initialize(
                "Enable ROM Hack",
                "Disable ROM Hack",
                () => Config.VarHack.ShowVarRomHack.LoadPayload(),
                () => Config.VarHack.ShowVarRomHack.ClearPayload(),
                () => Config.VarHack.ShowVarRomHack.Enabled);



            _textBoxXPosValue = splitContainerVarHack.Panel1.Controls["textBoxXPosValue"] as TextBox;
            InitializePositionControls(
                _textBoxXPosValue,
                splitContainerVarHack.Panel1.Controls["textBoxXPosChange"] as TextBox,
                splitContainerVarHack.Panel1.Controls["buttonXPosSubtract"] as Button,
                splitContainerVarHack.Panel1.Controls["buttonXPosAdd"] as Button);

            _textBoxYPosValue = splitContainerVarHack.Panel1.Controls["textBoxYPosValue"] as TextBox;
            InitializePositionControls(
                _textBoxYPosValue,
                splitContainerVarHack.Panel1.Controls["textBoxYPosChange"] as TextBox,
                splitContainerVarHack.Panel1.Controls["buttonYPosSubtract"] as Button,
                splitContainerVarHack.Panel1.Controls["buttonYPosAdd"] as Button);

            _textBoxYDeltaValue = splitContainerVarHack.Panel1.Controls["textBoxYDeltaValue"] as TextBox;
            InitializePositionControls(
                _textBoxYDeltaValue,
                splitContainerVarHack.Panel1.Controls["textBoxYDeltaChange"] as TextBox,
                splitContainerVarHack.Panel1.Controls["buttonYDeltaSubtract"] as Button,
                splitContainerVarHack.Panel1.Controls["buttonYDeltaAdd"] as Button);
        }

        private void InitializePositionControls(
            TextBox valueTextbox,
            TextBox changeTextbox,
            Button subtractButton,
            Button addButton)
        {
            subtractButton.Click += (sender, e) =>
            {
                int? change = ParsingUtilities.ParseIntNullable(changeTextbox.Text);
                if (!change.HasValue) return;
                int? oldValue = ParsingUtilities.ParseIntNullable(valueTextbox.Text);
                if (!oldValue.HasValue) return;
                int newValue = oldValue.Value - change.Value;
                valueTextbox.Text = newValue.ToString();
            };

            addButton.Click += (sender, e) =>
            {
                int? change = ParsingUtilities.ParseIntNullable(changeTextbox.Text);
                if (!change.HasValue) return;
                int? oldValue = ParsingUtilities.ParseIntNullable(valueTextbox.Text);
                if (!oldValue.HasValue) return;
                int newValue = oldValue.Value + change.Value;
                valueTextbox.Text = newValue.ToString();
            };
        }

        private void UpdatePositionsAndApplyVariablesToMemory()
        {
            int? xPos = ParsingUtilities.ParseIntNullable(_textBoxXPosValue.Text);
            int? yPos = ParsingUtilities.ParseIntNullable(_textBoxYPosValue.Text);
            int? yDelta = ParsingUtilities.ParseIntNullable(_textBoxYDeltaValue.Text);
            if (!xPos.HasValue || !yPos.HasValue || !yDelta.HasValue) return;
            _varHackPanel.SetPositions(xPos.Value, yPos.Value, yDelta.Value);
            _varHackPanel.ApplyVariablesToMemory();
        }

        public void Update(bool updateView)
        {
            if (!updateView) return;

            _buttonEnableDisableRomHack.UpdateButton();
        }

        public void AddVariable(string varName, uint address, Type memoryType, bool useHex, uint? pointerOffset)
        {
            _varHackPanel.AddNewControlWithParameters(varName, address, memoryType, useHex, pointerOffset);
        }
    }
}
