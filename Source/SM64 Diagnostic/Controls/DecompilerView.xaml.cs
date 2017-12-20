﻿using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace SM64_Diagnostic.Controls
{
    /// <summary>
    /// Interaction logic for DecompilerView.xaml
    /// </summary>
    public partial class DecompilerView : UserControl
    {
        static readonly IHighlightingDefinition _pythonSyntax;

        static DecompilerView()
        {
            // Load python syntax
            var asm = Assembly.GetExecutingAssembly();
            using (StreamReader stream = new StreamReader(
                asm.GetManifestResourceStream("SM64_Diagnostic.EmbeddedResources.Python.xshd")))
            {
                _pythonSyntax = HighlightingLoader.Load(new XmlTextReader(stream),
                HighlightingManager.Instance);
            }
        }

        public DecompilerView()
        {
            InitializeComponent();
        }

        public string Text
        {
            set
            {
                textEditor.Text = value;
            }
        }

        private void textEditor_Initialized(object sender, EventArgs e)
        {
            textEditor.SyntaxHighlighting = _pythonSyntax;
        }
    }
}
