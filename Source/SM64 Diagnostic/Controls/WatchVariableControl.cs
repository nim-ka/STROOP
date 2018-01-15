﻿using SM64_Diagnostic.Extensions;
using SM64_Diagnostic.Structs;
using SM64_Diagnostic.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SM64_Diagnostic.Controls
{
    public class WatchVariableControl : TableLayoutPanel
    {
        // Main objects
        private readonly WatchVariableControlPrecursor _watchVarPrecursor;
        private readonly WatchVariableWrapper _watchVarWrapper;
        public readonly List<VariableGroup> GroupList;

        // Sub controls
        private readonly Panel _namePanel;
        private readonly TextBox _nameTextBox;
        private readonly TextBox _valueTextBox;
        private readonly CheckBox _valueCheckBox;
        private readonly ContextMenuStrip _valueTextboxOriginalContextMenuStrip;
        private readonly ContextMenuStrip _nameTextboxOriginalContextMenuStrip;

        private readonly PictureBox _lockPictureBox;

        public string TextBoxValue
        {
            get { return _valueTextBox.Text; }
            set { _valueTextBox.Text = value; }
        }

        public CheckState CheckBoxValue
        {
            get { return _valueCheckBox.CheckState; }
            set { _valueCheckBox.CheckState = value; }
        }

        private static readonly Pen _borderPen = new Pen(Color.Red, 5);
        private static readonly int FAILURE_DURATION_MS = 1000;
        private static readonly Color FAILURE_COLOR = Color.Red;
        private static readonly Color DEFAULT_COLOR = SystemColors.Control;

        private Color _baseColor;
        public Color BaseColor
        {
            get { return _baseColor; }
            set { _baseColor = value; _currentColor = value; }
        }

        private Color _currentColor;
        private bool _justFailed;
        private DateTime _lastFailureTime;

        private string _varName;
        public string VarName
        {
            get
            {
                return _varName;
            }
        }

        private bool _showBorder;
        public bool ShowBorder
        {
            get
            {
                return _showBorder;
            }
            set
            {
                if (_showBorder == value)
                    return;

                _showBorder = value;
                Invalidate();
            }
        }

        private bool _editMode;
        public bool EditMode
        {
            get
            {
                return _editMode;
            }
            set
            {
                _editMode = value;
                _valueTextBox.ReadOnly = !_editMode;
                _valueTextBox.BackColor = _editMode ? Color.White : _currentColor;
                _valueTextBox.ContextMenuStrip = _editMode ? _valueTextboxOriginalContextMenuStrip : ContextMenuStrip;
                if (_editMode)
                {
                    _valueTextBox.Focus();
                    _valueTextBox.SelectAll();
                }
            }
        }

        private bool _renameMode;
        public bool RenameMode
        {
            get
            {
                return _renameMode;
            }
            set
            {
                _renameMode = value;
                _nameTextBox.ReadOnly = !_renameMode;
                _nameTextBox.BackColor = _renameMode ? Color.White : _currentColor;
                _nameTextBox.ContextMenuStrip = _renameMode ? _nameTextboxOriginalContextMenuStrip : ContextMenuStrip;
                if (_renameMode)
                {
                    _nameTextBox.Focus();
                    _nameTextBox.SelectAll();
                }
            }
        }

        public List<uint> FixedAddressList;

        private static Image _lockedImage = Properties.Resources._lock;
        private static Image _someLockedImage = Properties.Resources.lock_grey;
        private static Image _pinnedImage = Properties.Resources.pin;

        public static readonly int DEFAULT_VARIABLE_NAME_WIDTH = 120;
        public static readonly int DEFAULT_VARIABLE_VALUE_WIDTH = 80;
        public static readonly int DEFAULT_VARIABLE_HEIGHT = 20;

        public static int VariableNameWidth = DEFAULT_VARIABLE_NAME_WIDTH;
        public static int VariableValueWidth = DEFAULT_VARIABLE_VALUE_WIDTH;
        public static int VariableHeight = DEFAULT_VARIABLE_HEIGHT;

        private int _variableNameWidth;
        private int _variableValueWidth;
        private int _variableHeight;

        public WatchVariableControl(
            WatchVariableControlPrecursor watchVarPrecursor,
            string name,
            WatchVariable watchVar,
            WatchVariableSubclass subclass,
            Color? backgroundColor,
            bool? useHex,
            bool? invertBool,
            WatchVariableCoordinate? coordinate,
            List<VariableGroup> groupList)
        {
            // Store the precursor
            _watchVarPrecursor = watchVarPrecursor;

            // Initialize main fields
            _varName = name;
            GroupList = groupList;
            _showBorder = false;
            _editMode = false;
            _renameMode = false;
            FixedAddressList = null;

            // Initialize color fields
            _baseColor = backgroundColor ?? DEFAULT_COLOR;
            _currentColor = _baseColor;
            _justFailed = false;
            _lastFailureTime = DateTime.Now;

            // Initialize size fields
            _variableNameWidth = VariableNameWidth;
            _variableValueWidth = VariableValueWidth;
            _variableHeight = VariableHeight;

            // Initialize control fields
            InitializeBase();
            _namePanel = CreateNamePanel();
            _nameTextBox = CreateNameTextBox();
            //_lockPictureBox = CreateLockPictureBox();
            _valueTextBox = CreateValueTextBox();
            _valueCheckBox = CreateValueCheckBox();
            //base.Controls.Add(_lockPictureBox, 0, 0);
            base.Controls.Add(_valueTextBox, 1, 0);
            base.Controls.Add(_valueCheckBox, 1, 0);

            base.Controls.Add(_namePanel, 0, 0);
            _namePanel.Controls.Add(_nameTextBox);

            // Create var x
            _watchVarWrapper = WatchVariableWrapper.CreateWatchVariableWrapper(watchVar, this, subclass, useHex, invertBool, coordinate);

            // Initialize context menu strip
            _valueTextboxOriginalContextMenuStrip = _valueTextBox.ContextMenuStrip;
            _nameTextboxOriginalContextMenuStrip = _nameTextBox.ContextMenuStrip;
            ContextMenuStrip = _watchVarWrapper.GetContextMenuStrip();
            _nameTextBox.ContextMenuStrip = ContextMenuStrip;
            _valueTextBox.ContextMenuStrip = ContextMenuStrip;

            // Set whether to start as a checkbox
            SetUseCheckbox(_watchVarWrapper.StartsAsCheckbox());

            // Add functions
            _nameTextBox.Click += (sender, e) => OnNameTextBoxClick();
            _nameTextBox.Leave += (sender, e) => { RenameMode = false; };
            _nameTextBox.KeyDown += (sender, e) => OnNameTextValueKeyDown(e);
            _valueTextBox.DoubleClick += (sender, e) => { EditMode = true; };
            _valueTextBox.KeyDown += (sender, e) => OnValueTextValueKeyDown(e);
            _valueTextBox.Leave += (sender, e) => { EditMode = false; };
            _valueCheckBox.Click += (sender, e) => OnCheckboxClick();
        }

        private void InitializeBase()
        {
            base.Size = new Size(_variableNameWidth + _variableValueWidth, _variableHeight + 2);
            base.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            base.RowCount = 1;
            base.ColumnCount = 2;
            base.RowStyles.Clear();
            base.RowStyles.Add(new RowStyle(SizeType.Absolute, _variableHeight));
            base.ColumnStyles.Clear();
            base.Margin = new Padding(0);
            base.Padding = new Padding(0);
            base.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _variableNameWidth));
            base.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _variableValueWidth));
            base.BackColor = _currentColor;
        }

        private Panel CreateNamePanel()
        {
            Panel namePanel = new Panel();
            namePanel.Dock = DockStyle.Fill;
            namePanel.Margin = new Padding(0, 0, 0, 0);
            namePanel.BackColor = Color.Transparent;
            return namePanel;
        }

        private TextBox CreateNameTextBox()
        {
            TextBox nameTextBox = new TextBox();
            nameTextBox.Text = VarName;
            nameTextBox.Cursor = Cursors.Default;
            nameTextBox.ReadOnly = true;
            nameTextBox.BorderStyle = BorderStyle.None;
            nameTextBox.TextAlign = HorizontalAlignment.Left;
            nameTextBox.Anchor = AnchorStyles.Left;
            nameTextBox.Size = new Size(200, 20);
            nameTextBox.Location = new Point(4, _variableHeight / 2 - 7);
            return nameTextBox;
        }

        private PictureBox CreateLockPictureBox()
        {
            PictureBox lockPictureBox = new PictureBox();
            lockPictureBox.Image = Properties.Resources._lock;
            lockPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            lockPictureBox.Size = new Size(18, 18);
            lockPictureBox.Margin = new Padding(0, 0, 3, 0);
            lockPictureBox.Anchor = AnchorStyles.Right;
            return lockPictureBox;
        }

        private TextBox CreateValueTextBox()
        {
            TextBox nameTextBox = new TextBox();
            nameTextBox.ReadOnly = true;
            nameTextBox.BorderStyle = BorderStyle.None;
            nameTextBox.TextAlign = HorizontalAlignment.Right;
            nameTextBox.Margin = new Padding(0, 0, 6, 0);
            nameTextBox.Anchor = AnchorStyles.Right;
            nameTextBox.Size = new Size(200, 20);
            return nameTextBox;
        }

        private CheckBox CreateValueCheckBox()
        {
            CheckBox valueCheckBox = new CheckBox();
            valueCheckBox.CheckState = CheckState.Unchecked;
            valueCheckBox.BackColor = Color.Transparent;
            valueCheckBox.AutoSize = true;
            valueCheckBox.CheckAlign = ContentAlignment.MiddleRight;
            valueCheckBox.Dock = DockStyle.Fill;
            valueCheckBox.Margin = new Padding(0, 0, 5, 0);
            return valueCheckBox;
        }

        public void SetUseCheckbox(bool useCheckbox)
        {
            if (useCheckbox)
            {
                _valueTextBox.Visible = false;
                _valueCheckBox.Visible = true;
            }
            else
            {
                _valueTextBox.Visible = true;
                _valueCheckBox.Visible = false;
            }
        }

        private void OnValueTextValueKeyDown(KeyEventArgs e)
        {
            if (_editMode)
            {
                if (e.KeyData == Keys.Escape)
                {
                    EditMode = false;
                    this.Focus();
                    return;
                }

                if (e.KeyData == Keys.Enter)
                {
                    bool success = _watchVarWrapper.SetStringValue(_valueTextBox.Text, FixedAddressList);
                    EditMode = false;
                    if (!success) InvokeFailure();
                    this.Focus();
                    return;
                }
            }
        }

        private void OnNameTextBoxClick()
        {
            this.Focus();
            _watchVarWrapper.ShowVarInfo();
        }

        private void OnNameTextValueKeyDown(KeyEventArgs e)
        {
            if (_renameMode)
            {
                if (e.KeyData == Keys.Escape)
                {
                    RenameMode = false;
                    _nameTextBox.Text = VarName;
                    this.Focus();
                    return;
                }

                if (e.KeyData == Keys.Enter)
                {
                    _varName = _nameTextBox.Text;
                    RenameMode = false;
                    this.Focus();
                    return;
                }
            }
        }

        private void OnCheckboxClick()
        {
            bool success = _watchVarWrapper.SetCheckStateValue(_valueCheckBox.CheckState, FixedAddressList);
            if (!success) InvokeFailure();
        }

        public void UpdateControl()
        {
            if (!EditMode)
            {
                if (_valueTextBox.Visible) _valueTextBox.Text = _watchVarWrapper.GetStringValue(true, true, FixedAddressList);
                if (_valueCheckBox.Visible) _valueCheckBox.CheckState = _watchVarWrapper.GetCheckStateValue(FixedAddressList);
            }

            _watchVarWrapper.UpdateItemCheckStates();
            //_nameTextBox.Image = GetImageForCheckState(_watchVarWrapper.GetLockedCheckState(FixedAddressList));

            UpdateColor();
            UpdateSize();
        }

        private static Image GetImageForCheckState(CheckState checkState)
        {
            switch (checkState)
            {
                case CheckState.Unchecked:
                    return null;
                case CheckState.Checked:
                    return _lockedImage;
                case CheckState.Indeterminate:
                    return _someLockedImage;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateColor()
        {
            if (_justFailed)
            {
                DateTime currentTime = DateTime.Now;
                double timeSinceLastFailure = currentTime.Subtract(_lastFailureTime).TotalMilliseconds;
                if (timeSinceLastFailure < FAILURE_DURATION_MS)
                {
                    _currentColor = ColorUtilities.InterpolateColor(
                        FAILURE_COLOR, _baseColor, timeSinceLastFailure / FAILURE_DURATION_MS);
                }
                else
                {
                    _currentColor = _baseColor;
                    _justFailed = false;
                }
            }

            BackColor = _currentColor;
            if (!_editMode) _valueTextBox.BackColor = _currentColor;
            if (!_renameMode) _nameTextBox.BackColor = _currentColor;
        }

        private void UpdateSize()
        {
            if (_variableNameWidth == VariableNameWidth &&
                _variableValueWidth == VariableValueWidth &&
                _variableHeight == VariableHeight)
                return;

            _variableNameWidth = VariableNameWidth;
            _variableValueWidth = VariableValueWidth;
            _variableHeight = VariableHeight;

            Size = new Size(_variableNameWidth + _variableValueWidth, _variableHeight + 2);
            RowStyles[0].Height = _variableHeight;
            ColumnStyles[0].Width = _variableNameWidth;
            ColumnStyles[1].Width = _variableValueWidth;
        }

        private void InvokeFailure()
        {
            _justFailed = true;
            _lastFailureTime = DateTime.Now;
        }

        public bool BelongsToGroup(VariableGroup variableGroup)
        {
            return GroupList.Contains(variableGroup);
        }

        public bool BelongsToAnyGroup(List<VariableGroup> variableGroups)
        {
            return variableGroups.Any(varGroup => BelongsToGroup(varGroup));
        }

        public void NotifyPanel(WatchVariablePanel panel)
        {
            _watchVarWrapper.NotifyPanel(panel);
        }

        /*
        public void NotifyFiltering(List<ToolStripMenuItem> items, Action updateFunction)
        {
            _watchVarWrapper.NotifyFiltering(items, updateFunction);
        }
        */

        public void NotifyInCustomTab()
        {
            _watchVarWrapper.NotifyInCustomTab();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rec = DisplayRectangle;
            rec.Width -= 1;
            rec.Height -= 1;
            if (_showBorder)
                e.Graphics.DrawRectangle(_borderPen, rec);
        }

        public WatchVariableControl CreateCopy()
        {
            return _watchVarPrecursor.CreateWatchVariableControl();
        }
    }
}
