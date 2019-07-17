﻿using STROOP.Forms;
using STROOP.Managers;
using STROOP.Structs.Configurations;
using STROOP.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STROOP.Structs
{
    public static class CalculatorUtilities
    {
        public static List<Input> GetAllInputs()
        {
            return GetInputRange(-128, 127, -128, 127);
        }

        public static List<Input> GetInputRange(int minX, int maxX, int minZ, int maxZ)
        {
            List<Input> output = new List<Input>();
            for (int x = minX; x <= maxX; x++)
            {
                if (MoreMath.InputIsInDeadZone(x)) continue;
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (MoreMath.InputIsInDeadZone(z)) continue;
                    output.Add(new Input(x, z));
                }
            }
            return output;
        }
    }
}
