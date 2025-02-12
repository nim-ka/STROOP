﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using OpenTK.Graphics.OpenGL;
using STROOP.Utilities;
using STROOP.Structs.Configurations;
using STROOP.Structs;
using OpenTK;
using System.Drawing.Imaging;
using STROOP.Models;
using System.Windows.Forms;

namespace STROOP.Map
{
    public class MapObjectCeilingObject : MapCeilingObject
    {
        private readonly PositionAngle _posAngle;
        private readonly uint _objAddress;

        public MapObjectCeilingObject(uint objAddress)
            : base()
        {
            _posAngle = PositionAngle.Obj(objAddress);
            _objAddress = objAddress;
        }

        protected override List<TriangleDataModel> GetUnfilteredTriangles()
        {
            return TriangleUtilities.GetObjectTrianglesForObject(_objAddress)
                .FindAll(tri => tri.IsCeiling());
        }

        public override string GetName()
        {
            return "Ceiling Tris for " + PositionAngle.GetMapNameForObject(_objAddress);
        }

        public override Image GetInternalImage()
        {
            return Config.ObjectAssociations.TriangleCeilingImage;
        }

        public override ContextMenuStrip GetContextMenuStrip()
        {
            if (_contextMenuStrip == null)
            {
                _contextMenuStrip = new ContextMenuStrip();
                GetHorizontalTriangleToolStripMenuItems().ForEach(item => _contextMenuStrip.Items.Add(item));
                _contextMenuStrip.Items.Add(new ToolStripSeparator());
                GetTriangleToolStripMenuItems().ForEach(item => _contextMenuStrip.Items.Add(item));
            }

            return _contextMenuStrip;
        }

        public override PositionAngle GetPositionAngle()
        {
            return _posAngle;
        }
    }
}
