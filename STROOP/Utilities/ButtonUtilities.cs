﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STROOP.Structs;
using STROOP.Extensions;
using STROOP.Structs.Configurations;
using STROOP.Managers;
using STROOP.Models;
using STROOP.Forms;
using System.Diagnostics;
using STROOP.Map;

namespace STROOP.Utilities
{
    public static class ButtonUtilities
    {
        private enum Change { SET, ADD, MULTIPLY };

        private static bool ChangeValues(List<PositionAngle> posAngles,
            float xValue, float yValue, float zValue, Change change, bool useRelative = false, bool handleScaling = true,
            (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (posAngles.Count() == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var posAngle in posAngles)
            {
                float currentXValue = xValue;
                float currentYValue = yValue;
                float currentZValue = zValue;

                bool ignoreX = false;
                bool ignoreY = false;
                bool ignoreZ = false;

                if (change == Change.ADD)
                {
                    if (handleScaling) HandleScaling(ref currentXValue, ref currentZValue);
                    HandleRelativeAngle(ref currentXValue, ref currentZValue, useRelative, posAngle.Angle);

                    if (currentXValue == 0) ignoreX = true;
                    if (currentYValue == 0) ignoreY = true;
                    if (currentZValue == 0) ignoreZ = true;

                    currentXValue += (float)posAngle.X;
                    currentYValue += (float)posAngle.Y;
                    currentZValue += (float)posAngle.Z;
                }

                if (change == Change.MULTIPLY)
                {
                    if (currentXValue == 1) ignoreX = true;
                    if (currentYValue == 1) ignoreY = true;
                    if (currentZValue == 1) ignoreZ = true;

                    currentXValue *= (float)posAngle.X;
                    currentYValue *= (float)posAngle.Y;
                    currentZValue *= (float)posAngle.Z;
                }

                if ((!affects.HasValue || affects.Value.affectX) && !ignoreX)
                {
                    success &= posAngle.SetX(currentXValue);
                }

                if ((!affects.HasValue || affects.Value.affectY) && !ignoreY)
                {
                    success &= posAngle.SetY(currentYValue);
                }

                if ((!affects.HasValue || affects.Value.affectZ) && !ignoreZ)
                {
                    success &= posAngle.SetZ(currentZValue);
                }
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static void HandleScaling(ref float xOffset, ref float zOffset)
        {
            if (SavedSettingsConfig.ScaleDiagonalPositionControllerButtons)
            {
                (xOffset, zOffset) = ((float, float))MoreMath.ScaleValues(xOffset, zOffset);
            }
        }

        public static void HandleRelativeAngle(ref float xOffset, ref float zOffset, bool useRelative, double relativeAngle)
        {
            if (!useRelative) return;

            if (KeyboardUtilities.IsShiftHeld() || double.IsNaN(relativeAngle))
            {
                relativeAngle = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset);
            }
            else
            {
                if (!PositionControllerRelativityConfig.RelativityPA.IsNone())
                {
                    relativeAngle = PositionControllerRelativityConfig.RelativityPA.Angle;
                }
            }

            double thetaChange = MoreMath.NormalizeAngleDouble(relativeAngle - 32768);
            (xOffset, _, zOffset) = ((float, float, float))MoreMath.OffsetSpherically(xOffset, 0, zOffset, 0, thetaChange, 0);
        }

        public static bool GotoObjects(List<ObjectDataModel> objects, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (!objects.Any())
                return false;

            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Mario };

            float xDestination = objects.Average(o => o.X);
            float yDestination = objects.Average(o => o.Y);
            float zDestination = objects.Average(o => o.Z);

            HandleGotoOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET, affects: affects);
        }

        public static bool GotoObjectsCenter(List<ObjectDataModel> objects, bool lateralOnly)
        {
            if (!objects.Any())
                return false;

            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Mario };

            List<float> xMidpoints = new List<float>();
            List<float> yMaxes = new List<float>();
            List<float> zMidpoints = new List<float>();
            foreach (ObjectDataModel obj in objects)
            {
                List<TriangleDataModel> tris = TriangleUtilities.GetObjectTrianglesForObject(obj.Address);
                if (tris.Count == 0) continue;

                float xMin = tris.Min(tri => tri.GetMinX());
                float xMax = tris.Max(tri => tri.GetMaxX());
                float yMax = tris.Max(tri => tri.GetMaxY());
                float zMin = tris.Min(tri => tri.GetMinZ());
                float zMax = tris.Max(tri => tri.GetMaxZ());

                float xMidpoint = (xMin + xMax) / 2;
                float zMidpoint = (zMin + zMax) / 2;

                xMidpoints.Add(xMidpoint);
                yMaxes.Add(yMax);
                zMidpoints.Add(zMidpoint);
            }

            if (xMidpoints.Count == 0 || yMaxes.Count == 0 || zMidpoints.Count == 0) return false;

            float xDestination = xMidpoints.Average();
            float yDestination = yMaxes.Average();
            float zDestination = zMidpoints.Average();

            HandleGotoOffset(ref xDestination, ref yDestination, ref zDestination);

            (bool affectX, bool affectY, bool affectZ)? affects = lateralOnly ? (true, false, true) : (true, true, true);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET, affects: affects);
        }

        public static bool RetrieveObjects(List<ObjectDataModel> objects, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            List<PositionAngle> posAngles =
                objects.ConvertAll(o => PositionAngle.Obj(o.Address));

            float xDestination = DataModels.Mario.X;
            float yDestination = DataModels.Mario.Y;
            float zDestination = DataModels.Mario.Z;

            HandleRetrieveOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET, affects: affects);
        }

        private static void HandleGotoOffset(ref float xPos, ref float yPos, ref float zPos)
        {
            if (!SavedSettingsConfig.OffsetGotoRetrieveFunctions) return;
            float gotoAbove = GotoRetrieveConfig.GotoAboveOffset;
            float gotoInfront = GotoRetrieveConfig.GotoInfrontOffset;
            ushort marioYaw = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset);

            double xOffset, zOffset;
            (xOffset, zOffset) = MoreMath.GetComponentsFromVector(-1 * gotoInfront, marioYaw);

            xPos += (float)xOffset;
            yPos += gotoAbove;
            zPos += (float)zOffset;
        }

        private static void HandleRetrieveOffset(ref float xPos, ref float yPos, ref float zPos)
        {
            if (!SavedSettingsConfig.OffsetGotoRetrieveFunctions) return;
            float retrieveAbove = GotoRetrieveConfig.RetrieveAboveOffset;
            float retrieveInfront = GotoRetrieveConfig.RetrieveInfrontOffset;
            ushort marioYaw = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset);

            double xOffset, zOffset;
            (xOffset, zOffset) = MoreMath.GetComponentsFromVector(retrieveInfront, marioYaw);

            xPos += (float)xOffset;
            yPos += retrieveAbove;
            zPos += (float)zOffset;
        }

        public static bool TranslateObjects(List<ObjectDataModel> objects,
            float xOffset, float yOffset, float zOffset, bool useRelative, bool includeMario, bool includeHomes)
        {
            List<PositionAngle> posAngles =
                objects.ConvertAll(o => PositionAngle.Obj(o.Address));

            if (includeMario)
                posAngles.Add(PositionAngle.Mario);

            if (includeHomes)
                posAngles.AddRange(objects.ConvertAll(o => PositionAngle.ObjHome(o.Address)));

            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslateObjectHomes(List<ObjectDataModel> objects,
            float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<PositionAngle> posAngles =
                objects.ConvertAll(o => PositionAngle.ObjHome(o.Address));

            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool RotateObjects(List<ObjectDataModel> objects,
            int yawOffset, int pitchOffset, int rollOffset, bool includeMario, bool rotateAroundCenter)
        {
            if (!objects.Any())
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (ObjectDataModel obj in objects)
            {
                ushort yawFacing, pitchFacing, rollFacing, yawMoving, pitchMoving, rollMoving;
                yawFacing = Config.Stream.GetUInt16(obj.Address + ObjectConfig.YawFacingOffset);
                pitchFacing = Config.Stream.GetUInt16(obj.Address + ObjectConfig.PitchFacingOffset);
                rollFacing = Config.Stream.GetUInt16(obj.Address + ObjectConfig.RollFacingOffset);
                yawMoving = Config.Stream.GetUInt16(obj.Address + ObjectConfig.YawMovingOffset);
                pitchMoving = Config.Stream.GetUInt16(obj.Address + ObjectConfig.PitchMovingOffset);
                rollMoving = Config.Stream.GetUInt16(obj.Address + ObjectConfig.RollMovingOffset);

                yawFacing += (ushort)yawOffset;
                pitchFacing += (ushort)pitchOffset;
                rollFacing += (ushort)rollOffset;
                yawMoving += (ushort)yawOffset;
                pitchMoving += (ushort)pitchOffset;
                rollMoving += (ushort)rollOffset;

                success &= Config.Stream.SetValue(yawFacing, obj.Address + ObjectConfig.YawFacingOffset);
                success &= Config.Stream.SetValue(pitchFacing, obj.Address + ObjectConfig.PitchFacingOffset);
                success &= Config.Stream.SetValue(rollFacing, obj.Address + ObjectConfig.RollFacingOffset);
                success &= Config.Stream.SetValue(yawMoving, obj.Address + ObjectConfig.YawMovingOffset);
                success &= Config.Stream.SetValue(pitchMoving, obj.Address + ObjectConfig.PitchMovingOffset);
                success &= Config.Stream.SetValue(rollMoving, obj.Address + ObjectConfig.RollMovingOffset);
            }

            if (rotateAroundCenter && yawOffset != 0)
            {
                float centerX = objects.Average(obj => obj.X);
                float centerZ = objects.Average(obj => obj.Z);

                foreach (ObjectDataModel obj in objects)
                {
                    (double newX, double newZ) =
                        MoreMath.RotatePointAboutPointAnAngularDistance(
                            obj.X, obj.Z, centerX, centerZ, yawOffset);
                    success &= Config.Stream.SetValue((float)newX, obj.Address + ObjectConfig.XOffset);
                    success &= Config.Stream.SetValue((float)newZ, obj.Address + ObjectConfig.ZOffset);
                }
            }

            if (includeMario && yawOffset != 0)
            {
                PositionAngle obj = PositionAngle.Obj(objects[0].Address);
                PositionAngle mario = PositionAngle.Mario;
                double angleObjToMario = PositionAngle.GetAngleTo(obj, mario, null, false);
                double newAngleObjToMario = angleObjToMario + yawOffset;
                success &= PositionAngle.SetAngleTo(obj, mario, newAngleObjToMario, false);
                success &= mario.SetAngle(mario.Angle + yawOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ScaleObjects(List<ObjectDataModel> objects,
            float widthChange, float heightChange, float depthChange, bool multiply)
        {
            List<PositionAngle> posAngles = objects.ConvertAll(o => PositionAngle.ObjScale(o.Address));
            return ChangeValues(posAngles, widthChange, heightChange, depthChange, multiply ? Change.MULTIPLY : Change.ADD, handleScaling: false);
        }

        public static bool GotoObjectsHome(List<ObjectDataModel> objects, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (!objects.Any())
                return false;

            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Mario };

            float xDestination = objects.Average(o => o.HomeX);
            float yDestination = objects.Average(o => o.HomeY);
            float zDestination = objects.Average(o => o.HomeZ);

            HandleGotoOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET, affects: affects);
        }

        public static bool ObjectGotoObjectsHome(List<ObjectDataModel> objects, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (!objects.Any())
                return false;

            List<PositionAngle> objPoses = objects.ConvertAll(o => PositionAngle.Obj(o.Address));
            List<PositionAngle> objHomes = objects.ConvertAll(o => PositionAngle.ObjHome(o.Address));

            bool success = true;
            for (int i = 0; i < objPoses.Count; i++)
            {
                PositionAngle objPos = objPoses[i];
                PositionAngle objHome = objHomes[i];
                success &= ChangeValues(
                    new List<PositionAngle>() { objPos },
                    (float)objHome.X,
                    (float)objHome.Y,
                    (float)objHome.Z,
                    Change.SET,
                    affects: affects);
            }
            return success;
        }

        public static bool RetrieveObjectsHome(List<ObjectDataModel> objects, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (!objects.Any())
                return false;

            List<PositionAngle> posAngles =
                objects.ConvertAll(o => PositionAngle.ObjHome(o.Address));

            float xDestination = DataModels.Mario.X;
            float yDestination = DataModels.Mario.Y;
            float zDestination = DataModels.Mario.Z;

            HandleRetrieveOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET, affects: affects);
        }

        public static bool RetrieveObjectsHomeToObject(List<ObjectDataModel> objects, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (!objects.Any())
                return false;

            List<PositionAngle> objPoses = objects.ConvertAll(o => PositionAngle.Obj(o.Address));
            List<PositionAngle> objHomes = objects.ConvertAll(o => PositionAngle.ObjHome(o.Address));

            bool success = true;
            for (int i = 0; i < objPoses.Count; i++)
            {
                PositionAngle objPos = objPoses[i];
                PositionAngle objHome = objHomes[i];
                success &= ChangeValues(
                    new List<PositionAngle>() { objHome },
                    (float)objPos.X,
                    (float)objPos.Y,
                    (float)objPos.Z,
                    Change.SET,
                    affects: affects);
            }
            return success;
        }

        public static bool CloneObject(ObjectDataModel obj, bool updateAction = true)
        {
            if (obj == null)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            // Update action if going from not holding to holding
            if (updateAction && DataModels.Mario.HeldObject == 0)
            {
                DataModels.Mario.Action = TableConfig.MarioActions.GetAfterCloneValue(DataModels.Mario.Action);
            }

            // Update HOLP type if it's 0
            if (SavedSettingsConfig.CloningUpdatesHolpType)
            {
                success &= Config.Stream.SetValue((byte)1, MarioConfig.StructAddress + MarioConfig.HolpTypeOffset);
            }

            // Update held value
            success &= Config.Stream.SetValue(obj.Address, MarioConfig.StructAddress + MarioConfig.HeldObjectPointerOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnCloneObject(bool updateAction = true)
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            // Set mario's next action
            if (updateAction)
            {
                uint nextAction = TableConfig.MarioActions.GetAfterUncloneValue(DataModels.Mario.Action);
                DataModels.Mario.Action = nextAction;
            }

            // Clear mario's held object
            success &= Config.Stream.SetValue(0x00000000U, MarioConfig.StructAddress + MarioConfig.HeldObjectPointerOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool RideObject(ObjectDataModel obj, bool updateAction = true)
        {
            if (obj == null)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            if (updateAction)
            {
                DataModels.Mario.Action = MarioConfig.RidingShellAction;
            }

            success &= Config.Stream.SetValue(obj.Address, MarioConfig.StructAddress + MarioConfig.RiddenObjectPointerOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnRideObject(bool updateAction = true)
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            if (updateAction)
            {
                DataModels.Mario.Action = MarioConfig.IdleAction;
            }

            success &= Config.Stream.SetValue(0, MarioConfig.StructAddress + MarioConfig.RiddenObjectPointerOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }
        public static bool UkikipediaObject(ObjectDataModel obj)
        {
            if (obj == null)
                return false;

            Process.Start("https://ukikipedia.net/wiki/" + obj.BehaviorAssociation.Name);
            return true;
        }

        public static bool UnloadObject(List<ObjectDataModel> objects)
        {
            if (!objects.Any())
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var obj in objects)
                obj.IsActive = false;

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ReviveObject(List<ObjectDataModel> objects)
        {
            if (!objects.Any())
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var obj in objects)
            {
                byte? processGroup = obj.BehaviorProcessGroup;
                // Find process group
                if (!processGroup.HasValue)
                {
                    success = false;
                    break;
                }

                // Read first object in group
                uint groupAddress = ObjectSlotsConfig.ProcessGroupsStartAddress + processGroup.Value * ObjectSlotsConfig.ProcessGroupStructSize;

                // Loop through and find last object in group
                uint lastGroupObj = groupAddress;
                while (Config.Stream.GetUInt32(lastGroupObj + ObjectConfig.ProcessedNextLinkOffset) != groupAddress)
                    lastGroupObj = Config.Stream.GetUInt32(lastGroupObj + ObjectConfig.ProcessedNextLinkOffset);

                // Remove object from current group
                uint nextObj = Config.Stream.GetUInt32(obj.Address + ObjectConfig.ProcessedNextLinkOffset);
                uint prevObj = Config.Stream.GetUInt32(ObjectSlotsConfig.VacantSlotsNodeAddress + ObjectConfig.ProcessedNextLinkOffset);
                if (prevObj == obj.Address)
                {
                    // Set new vacant pointer
                    success &= Config.Stream.SetValue(nextObj, ObjectSlotsConfig.VacantSlotsNodeAddress + ObjectConfig.ProcessedNextLinkOffset);
                }
                else
                {
                    for (int i = 0; i < ObjectSlotsConfig.MaxSlots; i++)
                    {
                        uint curObj = Config.Stream.GetUInt32(prevObj + ObjectConfig.ProcessedNextLinkOffset);
                        if (curObj == obj.Address)
                            break;
                        prevObj = curObj;
                    }
                    success &= Config.Stream.SetValue(nextObj, prevObj + ObjectConfig.ProcessedNextLinkOffset);
                }

                // Insert object in new group
                nextObj = Config.Stream.GetUInt32(lastGroupObj + ObjectConfig.ProcessedNextLinkOffset);
                success &= Config.Stream.SetValue(obj.Address, nextObj + ObjectConfig.ProcessedPreviousLinkOffset);
                success &= Config.Stream.SetValue(obj.Address, lastGroupObj + ObjectConfig.ProcessedNextLinkOffset);
                success &= Config.Stream.SetValue(lastGroupObj, obj.Address + ObjectConfig.ProcessedPreviousLinkOffset);
                success &= Config.Stream.SetValue(nextObj, obj.Address + ObjectConfig.ProcessedNextLinkOffset);

                obj.IsActive = true;

                if (!Config.Stream.RefreshRam() || !success)
                    break;
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ReleaseObject(List<ObjectDataModel> objects, bool useThrownValue = true)
        {
            if (!objects.Any())
                return false;

            uint releasedValue = useThrownValue ? ObjectConfig.ReleaseStatusThrownValue : ObjectConfig.ReleaseStatusDroppedValue;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var obj in objects)
            {
                obj.ReleaseStatus = releasedValue;
                success &= Config.Stream.SetValue(ObjectConfig.StackIndexReleasedValue, obj.Address + ObjectConfig.StackIndexOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnReleaseObject(List<ObjectDataModel> objects)
        {
            if (!objects.Any())
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var obj in objects)
            {
                uint initialReleaseStatus = Config.Stream.GetUInt32(obj.Address + ObjectConfig.InitialReleaseStatusOffset);
                success &= Config.Stream.SetValue(initialReleaseStatus, obj.Address + ObjectConfig.ReleaseStatusOffset);
                success &= Config.Stream.SetValue(ObjectConfig.StackIndexUnReleasedValue, obj.Address + ObjectConfig.StackIndexOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool InteractObject(List<ObjectDataModel> objects)
        {
            if (!objects.Any())
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var obj in objects)
                obj.InteractionStatus = 0xFFFFFFFF;

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnInteractObject(List<ObjectDataModel> objects)
        {
            if (!objects.Any())
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var obj in objects)
                obj.InteractionStatus = 0x00000000;

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ToggleHandsfree()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            var heldObj = Config.Stream.GetUInt32(MarioConfig.StructAddress + MarioConfig.HeldObjectPointerOffset);

            if (heldObj != 0x00000000U)
            {
                uint currentAction = Config.Stream.GetUInt32(MarioConfig.StructAddress + MarioConfig.ActionOffset);
                uint nextAction = TableConfig.MarioActions.GetHandsfreeValue(currentAction);
                success = Config.Stream.SetValue(nextAction, MarioConfig.StructAddress + MarioConfig.ActionOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ToggleVisibility()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            var marioObjRef = Config.Stream.GetUInt32(MarioObjectConfig.PointerAddress);
            if (marioObjRef != 0x00000000U)
            {
                var marioGraphics = Config.Stream.GetUInt32(marioObjRef + ObjectConfig.BehaviorGfxOffset);
                if (marioGraphics == 0)
                {
                    success &= Config.Stream.SetValue(MarioObjectConfig.GraphicValue, marioObjRef + ObjectConfig.BehaviorGfxOffset);
                }
                else
                {
                    success &= Config.Stream.SetValue(0x00000000U, marioObjRef + ObjectConfig.BehaviorGfxOffset);
                }
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool TranslateMario(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Mario };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool SetMarioPosition(float xValue, float yValue, float zValue)
        {
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Mario };
            return ChangeValues(posAngles, xValue, yValue, zValue, Change.SET);
        }

        public static bool GotoHOLP((bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Mario };

            float xDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HolpXOffset);
            float yDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HolpYOffset);
            float zDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HolpZOffset);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET, affects: affects);
        }

        public static bool RetrieveHOLP((bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Holp };

            float xDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.XOffset);
            float yDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YOffset);
            float zDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.ZOffset);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET, affects: affects);
        }

        public static bool TranslateHOLP(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            PositionAngle holpPosMarioAngle = PositionAngle.Hybrid(PositionAngle.Holp, PositionAngle.Mario);
            List<PositionAngle> posAngles = new List<PositionAngle> { holpPosMarioAngle };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslatePosAngle(List<PositionAngle> posAngles, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool RetrieveSnow(int index)
        {
            List<PositionAngle> posAngles = new List<PositionAngle>() { PositionAngle.Snow(index) };

            float xDestination = DataModels.Mario.X;
            float yDestination = DataModels.Mario.Y;
            float zDestination = DataModels.Mario.Z;

            HandleRetrieveOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAngles, xDestination, yDestination, zDestination, Change.SET);
        }

        public static bool TranslateSnow(int index, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<PositionAngle> posAngles = new List<PositionAngle>() { PositionAngle.Snow(index) };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool MarioChangeYaw(int yawOffset)
        {
            ushort yaw = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset);
            yaw += (ushort)yawOffset;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(yaw, MarioConfig.StructAddress + MarioConfig.FacingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MarioChangeHspd(float hspdOffset)
        {
            float hspd = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HSpeedOffset);
            hspd += hspdOffset;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(hspd, MarioConfig.StructAddress + MarioConfig.HSpeedOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MarioChangeVspd(float vspdOffset)
        {
            float vspd = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YSpeedOffset);
            vspd += vspdOffset;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(vspd, MarioConfig.StructAddress + MarioConfig.YSpeedOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static void MarioChangeSlidingSpeedX(float xOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);

            float newSlidingSpeedX = slidingSpeedX + xOffset;
            ushort newSlidingSpeedYaw = MoreMath.AngleTo_AngleUnitsRounded(newSlidingSpeedX, slidingSpeedZ);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue(newSlidingSpeedX, MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            Config.Stream.SetValue(newSlidingSpeedYaw, MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static void MarioChangeSlidingSpeedZ(float zOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);

            float newSlidingSpeedZ = slidingSpeedZ + zOffset;
            ushort newSlidingSpeedYaw = MoreMath.AngleTo_AngleUnitsRounded(slidingSpeedX, newSlidingSpeedZ);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue(newSlidingSpeedZ, MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            Config.Stream.SetValue(newSlidingSpeedYaw, MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static void MarioChangeSlidingSpeedH(float hOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            double slidingSpeedH = MoreMath.GetHypotenuse(slidingSpeedX, slidingSpeedZ);

            double? slidingSpeedYawComputed = MoreMath.AngleTo_AngleUnitsNullable(slidingSpeedX, slidingSpeedZ);
            ushort slidingSpeedYawMemory = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);
            double slidingSpeedYaw = slidingSpeedYawComputed ?? slidingSpeedYawMemory;

            double newSlidingSpeedH = slidingSpeedH + hOffset;
            (double newSlidingSpeedX, double newSlidingSpeedZ) = MoreMath.GetComponentsFromVector(newSlidingSpeedH, slidingSpeedYaw);
            double newSlidingSpeedYaw = MoreMath.AngleTo_AngleUnits(newSlidingSpeedX, newSlidingSpeedZ);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue((float)newSlidingSpeedX, MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            Config.Stream.SetValue((float)newSlidingSpeedZ, MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(newSlidingSpeedYaw), MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static void MarioChangeSlidingSpeedYaw(float yawOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            double slidingSpeedH = MoreMath.GetHypotenuse(slidingSpeedX, slidingSpeedZ);

            double? slidingSpeedYawComputed = MoreMath.AngleTo_AngleUnitsNullable(slidingSpeedX, slidingSpeedZ);
            ushort slidingSpeedYawMemory = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);
            double slidingSpeedYaw = slidingSpeedYawComputed ?? slidingSpeedYawMemory;

            double newSlidingSpeedYaw = slidingSpeedYaw + yawOffset;
            (double newSlidingSpeedX, double newSlidingSpeedZ) = MoreMath.GetComponentsFromVector(slidingSpeedH, newSlidingSpeedYaw);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue((float)newSlidingSpeedX, MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            Config.Stream.SetValue((float)newSlidingSpeedZ, MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(newSlidingSpeedYaw), MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static bool FullHp()
        {
            return Config.Stream.SetValue(HudConfig.FullHp, MarioConfig.StructAddress + HudConfig.HpCountOffset);
        }

        public static bool Die()
        {
            return Config.Stream.SetValue(HudConfig.DeathHp, MarioConfig.StructAddress + HudConfig.HpCountOffset);
        }

        public static bool GameOver()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((sbyte)0, MarioConfig.StructAddress + HudConfig.LifeCountOffset);
            success &= Config.Stream.SetValue(HudConfig.DeathHp, MarioConfig.StructAddress + HudConfig.HpCountOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool StandardHud()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(HudConfig.FullHp, MarioConfig.StructAddress + HudConfig.HpCountOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardCoins, MarioConfig.StructAddress + HudConfig.CoinCountOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardLives, MarioConfig.StructAddress + HudConfig.LifeCountOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardStars, MarioConfig.StructAddress + HudConfig.StarCountOffset);

            success &= Config.Stream.SetValue(HudConfig.FullHpInt, MarioConfig.StructAddress + HudConfig.HpDisplayOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardCoins, MarioConfig.StructAddress + HudConfig.CoinDisplayOffset);
            success &= Config.Stream.SetValue((short)HudConfig.StandardLives, MarioConfig.StructAddress + HudConfig.LifeDisplayOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardStars, MarioConfig.StructAddress + HudConfig.StarDisplayOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool Coins99()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((short)99, MarioConfig.StructAddress + HudConfig.CoinCountOffset);
            success &= Config.Stream.SetValue((short)99, MarioConfig.StructAddress + HudConfig.CoinDisplayOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool CoinStar100()
        {
            uint spawnStar = RomVersionConfig.SwitchMap(0x802AB558, 0x802AACE4);
            InGameFunctionCall.WriteInGameFunctionCall(spawnStar, 6);
            return true;
        }

        public static bool Lives100()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((sbyte)100, MarioConfig.StructAddress + HudConfig.LifeCountOffset);
            success &= Config.Stream.SetValue((short)100, MarioConfig.StructAddress + HudConfig.LifeDisplayOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool GotoTriangleVertexClosest(uint triangleAddress, bool useMisalignmentOffset = false)
        {
            if (triangleAddress == 0) return false;
            int closestVertex = WatchVariableSpecialUtilities.GetClosestTriangleVertexIndex(triangleAddress);
            return GotoTriangleVertex(triangleAddress, closestVertex, useMisalignmentOffset);
        }

        public static bool GotoTriangleVertex(uint triangleAddress, int vertex, bool _useMisalignmentOffset = false)
        {
            if (triangleAddress == 0) return false;

            float newX, newY, newZ;
            switch (vertex)
            {
                case 1:
                    newX = TriangleOffsetsConfig.GetX1(triangleAddress);
                    newY = TriangleOffsetsConfig.GetY1(triangleAddress);
                    newZ = TriangleOffsetsConfig.GetZ1(triangleAddress);
                    break;

                case 2:
                    newX = TriangleOffsetsConfig.GetX2(triangleAddress);
                    newY = TriangleOffsetsConfig.GetY2(triangleAddress);
                    newZ = TriangleOffsetsConfig.GetZ2(triangleAddress);
                    break;

                case 3:
                    newX = TriangleOffsetsConfig.GetX3(triangleAddress);
                    newY = TriangleOffsetsConfig.GetY3(triangleAddress);
                    newZ = TriangleOffsetsConfig.GetZ3(triangleAddress);
                    break;

                default:
                    throw new Exception("There are only 3 vertices in a triangle. You are an idiot :).");
            }

            if (_useMisalignmentOffset)
            {
                newX += (newX >= 0) ? 0.5f : -0.5f;
                newZ += (newZ >= 0) ? 0.5f : -0.5f;
            }

            // Move mario to triangle (while in same Pu)
            return PuUtilities.SetMarioPositionInCurrentPu(newX, newY, newZ);
        }

        public static bool RetrieveTriangle(List<uint> triangleAddresses)
        {
            if (triangleAddresses.Count == 1 && triangleAddresses[0] == 0) return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (uint triangleAddress in triangleAddresses)
            {
                float normX, normY, normZ, oldNormOffset;
                normX = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormX);
                normY = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormY);
                normZ = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormZ);
                oldNormOffset = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormOffset);

                // Get Mario position
                float marioX, marioY, marioZ;
                marioX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.XOffset);
                marioY = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YOffset);
                marioZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.ZOffset);

                float normOffset = -(normX * marioX + normY * marioY + normZ * marioZ);
                float normDiff = normOffset - oldNormOffset;

                short yOffset = (short)(-normDiff * normY);

                short v1Y, v2Y, v3Y;
                v1Y = (short)(TriangleOffsetsConfig.GetY1(triangleAddress) + yOffset);
                v2Y = (short)(TriangleOffsetsConfig.GetY2(triangleAddress) + yOffset);
                v3Y = (short)(TriangleOffsetsConfig.GetY3(triangleAddress) + yOffset);

                short yMin = (short)(Math.Min(Math.Min(v1Y, v2Y), v3Y) - 5);
                short yMax = (short)(Math.Max(Math.Max(v1Y, v2Y), v3Y) + 5);

                success &= TriangleOffsetsConfig.SetY1(v1Y, triangleAddress);
                success &= TriangleOffsetsConfig.SetY2(v2Y, triangleAddress);
                success &= TriangleOffsetsConfig.SetY3(v3Y, triangleAddress);
                success &= Config.Stream.SetValue(yMin, triangleAddress + TriangleOffsetsConfig.YMinMinus5);
                success &= Config.Stream.SetValue(yMax, triangleAddress + TriangleOffsetsConfig.YMaxPlus5);
                success &= Config.Stream.SetValue(normOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool NeutralizeTriangle(List<uint> triangleAddresses, bool? use0x15Nullable = null)
        {
            if (triangleAddresses.Count == 1 && triangleAddresses[0] == 0) return false;

            short neutralizeValue = SavedSettingsConfig.NeutralizeTriangleValue(use0x15Nullable);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (uint triangleAddress in triangleAddresses)
            {
                success &= Config.Stream.SetValue(neutralizeValue, triangleAddress + TriangleOffsetsConfig.SurfaceType);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool DisableCamCollisionForTriangle(uint triangleAddress)
        {
            if (triangleAddress == 0x0000)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            byte oldFlags = Config.Stream.GetByte(triangleAddress + TriangleOffsetsConfig.Flags);
            byte newFlags = MoreMath.ApplyValueToMaskedByte(oldFlags, TriangleOffsetsConfig.NoCamCollisionMask, true);
            success &= Config.Stream.SetValue(newFlags, triangleAddress + TriangleOffsetsConfig.Flags);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool AnnihilateTriangle(List<uint> triangleAddresses)
        {
            if (triangleAddresses.Count == 1 && triangleAddresses[0] == 0) return false;

            short xzCoordinate = 16000;
            short yCoordinate = 30000;
            short v1X = xzCoordinate;
            short v1Y = yCoordinate;
            short v1Z = xzCoordinate;
            short v2X = xzCoordinate;
            short v2Y = yCoordinate;
            short v2Z = xzCoordinate;
            short v3X = xzCoordinate;
            short v3Y = yCoordinate;
            short v3Z = xzCoordinate;
            float normX = 0;
            float normY = 0;
            float normZ = 0;
            float normOffset = 16000;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (uint triangleAddress in triangleAddresses)
            {
                success &= TriangleOffsetsConfig.SetX1(v1X, triangleAddress);
                success &= TriangleOffsetsConfig.SetY1(v1Y, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ1(v1Z, triangleAddress);
                success &= TriangleOffsetsConfig.SetX2(v2X, triangleAddress);
                success &= TriangleOffsetsConfig.SetY2(v2Y, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ2(v2Z, triangleAddress);
                success &= TriangleOffsetsConfig.SetX3(v3X, triangleAddress);
                success &= TriangleOffsetsConfig.SetY3(v3Y, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ3(v3Z, triangleAddress);
                success &= Config.Stream.SetValue(normX, triangleAddress + TriangleOffsetsConfig.NormX);
                success &= Config.Stream.SetValue(normY, triangleAddress + TriangleOffsetsConfig.NormY);
                success &= Config.Stream.SetValue(normZ, triangleAddress + TriangleOffsetsConfig.NormZ);
                success &= Config.Stream.SetValue(normOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MoveTriangle(List<uint> triangleAddresses,
            float xOffsetBase, float yOffsetBase, float zOffsetBase, bool useRelative)
        {
            if (triangleAddresses.Count == 1 && triangleAddresses[0] == 0) return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (uint triangleAddress in triangleAddresses)
            {
                float xOffset = xOffsetBase;
                float yOffset = yOffsetBase;
                float zOffset = zOffsetBase;

                HandleScaling(ref xOffset, ref zOffset);

                float normX, normY, normZ, oldNormOffset;
                normX = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormX);
                normY = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormY);
                normZ = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormZ);
                oldNormOffset = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormOffset);

                ushort relativeAngle = MoreMath.getUphillAngle(normX, normY, normZ);
                HandleRelativeAngle(ref xOffset, ref zOffset, useRelative, relativeAngle);

                float newNormOffset = oldNormOffset - normX * xOffset - normY * yOffset - normZ * zOffset;

                short newX1, newY1, newZ1, newX2, newY2, newZ2, newX3, newY3, newZ3;
                newX1 = (short)(TriangleOffsetsConfig.GetX1(triangleAddress) + xOffset);
                newY1 = (short)(TriangleOffsetsConfig.GetY1(triangleAddress) + yOffset);
                newZ1 = (short)(TriangleOffsetsConfig.GetZ1(triangleAddress) + zOffset);
                newX2 = (short)(TriangleOffsetsConfig.GetX2(triangleAddress) + xOffset);
                newY2 = (short)(TriangleOffsetsConfig.GetY2(triangleAddress) + yOffset);
                newZ2 = (short)(TriangleOffsetsConfig.GetZ2(triangleAddress) + zOffset);
                newX3 = (short)(TriangleOffsetsConfig.GetX3(triangleAddress) + xOffset);
                newY3 = (short)(TriangleOffsetsConfig.GetY3(triangleAddress) + yOffset);
                newZ3 = (short)(TriangleOffsetsConfig.GetZ3(triangleAddress) + zOffset);

                short newYMin = (short)(Math.Min(Math.Min(newY1, newY2), newY3) - 5);
                short newYMax = (short)(Math.Max(Math.Max(newY1, newY2), newY3) + 5);

                success &= Config.Stream.SetValue(newNormOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);
                success &= TriangleOffsetsConfig.SetX1(newX1, triangleAddress);
                success &= TriangleOffsetsConfig.SetY1(newY1, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ1(newZ1, triangleAddress);
                success &= TriangleOffsetsConfig.SetX2(newX2, triangleAddress);
                success &= TriangleOffsetsConfig.SetY2(newY2, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ2(newZ2, triangleAddress);
                success &= TriangleOffsetsConfig.SetX3(newX3, triangleAddress);
                success &= TriangleOffsetsConfig.SetY3(newY3, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ3(newZ3, triangleAddress);
                success &= Config.Stream.SetValue(newYMin, triangleAddress + TriangleOffsetsConfig.YMinMinus5);
                success &= Config.Stream.SetValue(newYMax, triangleAddress + TriangleOffsetsConfig.YMaxPlus5);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MoveTriangleNormal(List<uint> triangleAddresses, float normalChange)
        {
            if (triangleAddresses.Count == 1 && triangleAddresses[0] == 0) return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (uint triangleAddress in triangleAddresses)
            {
                float normX, normY, normZ, oldNormOffset;
                normX = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormX);
                normY = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormY);
                normZ = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormZ);
                oldNormOffset = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormOffset);

                float newNormOffset = oldNormOffset - normalChange;

                double xChange = normalChange * normX;
                double yChange = normalChange * normY;
                double zChange = normalChange * normZ;

                short newX1, newY1, newZ1, newX2, newY2, newZ2, newX3, newY3, newZ3;
                newX1 = (short)(TriangleOffsetsConfig.GetX1(triangleAddress) + xChange);
                newY1 = (short)(TriangleOffsetsConfig.GetY1(triangleAddress) + yChange);
                newZ1 = (short)(TriangleOffsetsConfig.GetZ1(triangleAddress) + zChange);
                newX2 = (short)(TriangleOffsetsConfig.GetX2(triangleAddress) + xChange);
                newY2 = (short)(TriangleOffsetsConfig.GetY2(triangleAddress) + yChange);
                newZ2 = (short)(TriangleOffsetsConfig.GetZ2(triangleAddress) + zChange);
                newX3 = (short)(TriangleOffsetsConfig.GetX3(triangleAddress) + xChange);
                newY3 = (short)(TriangleOffsetsConfig.GetY3(triangleAddress) + yChange);
                newZ3 = (short)(TriangleOffsetsConfig.GetZ3(triangleAddress) + zChange);

                short newYMin = (short)(Math.Min(Math.Min(newY1, newY2), newY3) - 5);
                short newYMax = (short)(Math.Max(Math.Max(newY1, newY2), newY3) + 5);

                success &= Config.Stream.SetValue(newNormOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);
                success &= TriangleOffsetsConfig.SetX1(newX1, triangleAddress);
                success &= TriangleOffsetsConfig.SetY1(newY1, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ1(newZ1, triangleAddress);
                success &= TriangleOffsetsConfig.SetX2(newX2, triangleAddress);
                success &= TriangleOffsetsConfig.SetY2(newY2, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ2(newZ2, triangleAddress);
                success &= TriangleOffsetsConfig.SetX3(newX3, triangleAddress);
                success &= TriangleOffsetsConfig.SetY3(newY3, triangleAddress);
                success &= TriangleOffsetsConfig.SetZ3(newZ3, triangleAddress);
                success &= Config.Stream.SetValue(newYMin, triangleAddress + TriangleOffsetsConfig.YMinMinus5);
                success &= Config.Stream.SetValue(newYMax, triangleAddress + TriangleOffsetsConfig.YMaxPlus5);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool TranslateCamera(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.Camera };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslateCameraSpherically(float radiusOffset, float thetaOffset, float phiOffset, (float, float, float) pivotPoint)
        {
            float pivotX, pivotY, pivotZ;
            (pivotX, pivotY, pivotZ) = pivotPoint;

            HandleScaling(ref thetaOffset, ref phiOffset);

            float oldX, oldY, oldZ;
            oldX = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.XOffset);
            oldY = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.YOffset);
            oldZ = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.ZOffset);

            double newX, newY, newZ;
            (newX, newY, newZ) = MoreMath.OffsetSphericallyAboutPivot(oldX, oldY, oldZ, radiusOffset, thetaOffset, phiOffset, pivotX, pivotY, pivotZ);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((float)newX, CameraConfig.StructAddress + CameraConfig.XOffset);
            success &= Config.Stream.SetValue((float)newY, CameraConfig.StructAddress + CameraConfig.YOffset);
            success &= Config.Stream.SetValue((float)newZ, CameraConfig.StructAddress + CameraConfig.ZOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool TranslateCameraFocus(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.CameraFocus };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslateCameraFocusSpherically(float radiusOffset, float thetaOffset, float phiOffset)
        {
            float pivotX, pivotY, pivotZ;
            pivotX = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.XOffset);
            pivotY = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.YOffset);
            pivotZ = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.ZOffset);

            HandleScaling(ref thetaOffset, ref phiOffset);

            float oldX, oldY, oldZ;
            oldX = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.FocusXOffset);
            oldY = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.FocusYOffset);
            oldZ = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.FocusZOffset);

            double newX, newY, newZ;
            (newX, newY, newZ) = MoreMath.OffsetSphericallyAboutPivot(oldX, oldY, oldZ, radiusOffset, thetaOffset, phiOffset, pivotX, pivotY, pivotZ);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((float)newX, CameraConfig.StructAddress + CameraConfig.FocusXOffset);
            success &= Config.Stream.SetValue((float)newY, CameraConfig.StructAddress + CameraConfig.FocusYOffset);
            success &= Config.Stream.SetValue((float)newZ, CameraConfig.StructAddress + CameraConfig.FocusZOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        private static ushort getCamHackYawFacing(CamHackMode camHackMode)
        {
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                    return Config.Stream.GetUInt16(CameraConfig.StructAddress + CameraConfig.FacingYawOffset);

                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                case CamHackMode.FIXED_POS:
                case CamHackMode.FIXED_ORIENTATION:
                    float camHackPosX = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraXOffset);
                    float camHackPosZ = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraZOffset);
                    float camHackFocusX = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusXOffset);
                    float camHackFocusZ = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusZOffset);
                    return MoreMath.AngleTo_AngleUnitsRounded(camHackPosX, camHackPosZ, camHackFocusX, camHackFocusZ);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static PositionAngle GetCamHackFocusPosAngle(CamHackMode camHackMode)
        {
            uint camHackObject = Config.Stream.GetUInt32(CamHackConfig.StructAddress + CamHackConfig.ObjectOffset);
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                    return PositionAngle.Hybrid(PositionAngle.CameraFocus, PositionAngle.CamHackCamera);

                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                case CamHackMode.FIXED_POS:
                    if (camHackObject == 0) // focused on Mario
                    {
                        return PositionAngle.Hybrid(PositionAngle.Mario, PositionAngle.CamHackCamera);
                    }
                    else // focused on object
                    {
                        return PositionAngle.Hybrid(PositionAngle.Obj(camHackObject), PositionAngle.CamHackCamera);
                    }

                case CamHackMode.FIXED_ORIENTATION:
                    return PositionAngle.CamHackFocus;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool TranslateCameraHack(CamHackMode camHackMode, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                    {
                        return TranslateCamera(xOffset, yOffset, zOffset, useRelative);
                    }

                case CamHackMode.FIXED_POS:
                case CamHackMode.FIXED_ORIENTATION:
                    {
                        return ChangeValues(
                            new List<PositionAngle> { PositionAngle.CamHackCamera },
                            xOffset,
                            yOffset,
                            zOffset,
                            Change.ADD,
                            useRelative);
                    }

                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                    {
                        HandleScaling(ref xOffset, ref zOffset);

                        HandleRelativeAngle(ref xOffset, ref zOffset, useRelative, getCamHackYawFacing(camHackMode));
                        float xDestination = xOffset + Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraXOffset);
                        float yDestination = yOffset + Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraYOffset);
                        float zDestination = zOffset + Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraZOffset);

                        float xFocus = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusXOffset);
                        float yFocus = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusYOffset);
                        float zFocus = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusZOffset);

                        double radius, theta, height;
                        (radius, theta, height) = MoreMath.EulerToCylindricalAboutPivot(xDestination, yDestination, zDestination, xFocus, yFocus, zFocus);

                        ushort relativeYawOffset = 0;
                        if (camHackMode == CamHackMode.RELATIVE_ANGLE)
                        {
                            uint camHackObject = Config.Stream.GetUInt32(CamHackConfig.StructAddress + CamHackConfig.ObjectOffset);
                            relativeYawOffset = camHackObject == 0
                                ? Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset)
                                : Config.Stream.GetUInt16(camHackObject + ObjectConfig.YawFacingOffset);
                        }

                        bool success = true;
                        bool streamAlreadySuspended = Config.Stream.IsSuspended;
                        if (!streamAlreadySuspended) Config.Stream.Suspend();

                        success &= Config.Stream.SetValue((float)radius, CamHackConfig.StructAddress + CamHackConfig.RadiusOffset);
                        success &= Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(theta + 32768 - relativeYawOffset), CamHackConfig.StructAddress + CamHackConfig.ThetaOffset);
                        success &= Config.Stream.SetValue((float)height, CamHackConfig.StructAddress + CamHackConfig.RelativeHeightOffset);

                        if (!streamAlreadySuspended) Config.Stream.Resume();
                        return success;
                    }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool TranslateCameraHackSpherically(CamHackMode camHackMode, float radiusOffset, float thetaOffset, float phiOffset)
        {
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                    {
                        float xFocus = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.FocusXOffset);
                        float yFocus = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.FocusYOffset);
                        float zFocus = Config.Stream.GetSingle(CameraConfig.StructAddress + CameraConfig.FocusZOffset);
                        return TranslateCameraSpherically(radiusOffset, thetaOffset, phiOffset, (xFocus, yFocus, zFocus));
                    }

                case CamHackMode.FIXED_POS:
                case CamHackMode.FIXED_ORIENTATION:
                    {
                        HandleScaling(ref thetaOffset, ref phiOffset);

                        PositionAngle focusPosAngle = GetCamHackFocusPosAngle(camHackMode);
                        float xFocus = (float)focusPosAngle.X;
                        float yFocus = (float)focusPosAngle.Y;
                        float zFocus = (float)focusPosAngle.Z;

                        float xCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraXOffset);
                        float yCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraYOffset);
                        float zCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraZOffset);

                        double xDestination, yDestination, zDestination;
                        (xDestination, yDestination, zDestination) =
                            MoreMath.OffsetSphericallyAboutPivot(xCamPos, yCamPos, zCamPos, radiusOffset, thetaOffset, phiOffset, xFocus, yFocus, zFocus);

                        return ChangeValues(
                            new List<PositionAngle> { PositionAngle.CamHackCamera },
                            (float)xDestination,
                            (float)yDestination,
                            (float)zDestination,
                            Change.SET);
                    }

                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                    {
                        HandleScaling(ref thetaOffset, ref phiOffset);

                        float xCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraXOffset);
                        float yCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraYOffset);
                        float zCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraZOffset);

                        float xFocus = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusXOffset);
                        float yFocus = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusYOffset);
                        float zFocus = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.FocusZOffset);

                        double xDestination, yDestination, zDestination;
                        (xDestination, yDestination, zDestination) =
                            MoreMath.OffsetSphericallyAboutPivot(xCamPos, yCamPos, zCamPos, radiusOffset, thetaOffset, phiOffset, xFocus, yFocus, zFocus);

                        double radius, theta, height;
                        (radius, theta, height) = MoreMath.EulerToCylindricalAboutPivot(xDestination, yDestination, zDestination, xFocus, yFocus, zFocus);

                        ushort relativeYawOffset = 0;
                        if (camHackMode == CamHackMode.RELATIVE_ANGLE)
                        {
                            uint camHackObject = Config.Stream.GetUInt32(CamHackConfig.StructAddress + CamHackConfig.ObjectOffset);
                            relativeYawOffset = camHackObject == 0
                                ? Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset)
                                : Config.Stream.GetUInt16(camHackObject + ObjectConfig.YawFacingOffset);
                        }

                        bool success = true;
                        bool streamAlreadySuspended = Config.Stream.IsSuspended;
                        if (!streamAlreadySuspended) Config.Stream.Suspend();

                        success &= Config.Stream.SetValue((float)radius, CamHackConfig.StructAddress + CamHackConfig.RadiusOffset);
                        success &= Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(theta + 32768 - relativeYawOffset), CamHackConfig.StructAddress + CamHackConfig.ThetaOffset);
                        success &= Config.Stream.SetValue((float)height, CamHackConfig.StructAddress + CamHackConfig.RelativeHeightOffset);

                        if (!streamAlreadySuspended) Config.Stream.Resume();
                        return success;
                    }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool TranslateCameraHackFocus(CamHackMode camHackMode, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            return ChangeValues(
                new List<PositionAngle> { GetCamHackFocusPosAngle(camHackMode) },
                xOffset,
                yOffset,
                zOffset,
                Change.ADD,
                useRelative);
        }

        public static bool TranslateCameraHackFocusSpherically(CamHackMode camHackMode, float radiusOffset, float thetaOffset, float phiOffset)
        {
            HandleScaling(ref thetaOffset, ref phiOffset);

            PositionAngle focusPosAngle = GetCamHackFocusPosAngle(camHackMode);
            float xFocus = (float)focusPosAngle.X;
            float yFocus = (float)focusPosAngle.Y;
            float zFocus = (float)focusPosAngle.Z;

            float xCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraXOffset);
            float yCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraYOffset);
            float zCamPos = Config.Stream.GetSingle(CamHackConfig.StructAddress + CamHackConfig.CameraZOffset);

            double xDestination, yDestination, zDestination;
            (xDestination, yDestination, zDestination) =
                MoreMath.OffsetSphericallyAboutPivot(xFocus, yFocus, zFocus, radiusOffset, thetaOffset, phiOffset, xCamPos, yCamPos, zCamPos);

            return ChangeValues(
                new List<PositionAngle> { focusPosAngle },
                (float)xDestination,
                (float)yDestination,
                (float)zDestination,
                Change.SET);
        }

        public static bool TranslateCameraHackBoth(CamHackMode camHackMode, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            if (camHackMode != CamHackMode.RELATIVE_ANGLE && camHackMode != CamHackMode.ABSOLUTE_ANGLE)
            {
                success &= TranslateCameraHack(camHackMode, xOffset, yOffset, zOffset, useRelative);
            }
            success &= TranslateCameraHackFocus(camHackMode, xOffset, yOffset, zOffset, useRelative);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool SetHudVisibility(bool setHudOn, bool changeLevelIndex = true)
        {
            byte currentHudVisibility = Config.Stream.GetByte(MarioConfig.StructAddress + HudConfig.VisibilityOffset);
            byte newHudVisibility = MoreMath.ApplyValueToMaskedByte(currentHudVisibility, HudConfig.VisibilityMask, setHudOn);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(newHudVisibility, MarioConfig.StructAddress + HudConfig.VisibilityOffset);

            if (changeLevelIndex)
            {
                success &= Config.Stream.SetValue((short)(setHudOn ? 1 : 0), MiscConfig.LevelIndexAddress);
            }
            else
            {
                if (setHudOn)
                {
                    success &= Config.Stream.SetValue(0, HudConfig.FunctionDisableCoinDisplayAddress);
                }
                else
                {
                    success &= Config.Stream.SetValue(0, HudConfig.FunctionEnableCoinDisplayAddress);
                }
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool TranslateMapCameraPosition(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            MapUtilities.MaybeChangeMapCameraMode();
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.MapCamera };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslateMapCameraSpherical(float radiusOffset, float thetaOffset, float phiOffset)
        {
            MapUtilities.MaybeChangeMapCameraMode();
            HandleScaling(ref thetaOffset, ref phiOffset);

            (double newX, double newY, double newZ) =
                MoreMath.OffsetSphericallyAboutPivot(
                    SpecialConfig.Map3DCameraX, SpecialConfig.Map3DCameraY, SpecialConfig.Map3DCameraZ,
                    radiusOffset, thetaOffset, phiOffset,
                    SpecialConfig.Map3DFocusX, SpecialConfig.Map3DFocusY, SpecialConfig.Map3DFocusZ);

            SpecialConfig.Map3DCameraX = (float)newX;
            SpecialConfig.Map3DCameraY = (float)newY;
            SpecialConfig.Map3DCameraZ = (float)newZ;

            return true;
        }

        public static bool TranslateMapFocusPosition(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            MapUtilities.MaybeChangeMapCameraMode();
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.MapFocus };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslateMapFocusSpherical(float radiusOffset, float thetaOffset, float phiOffset)
        {
            MapUtilities.MaybeChangeMapCameraMode();
            HandleScaling(ref thetaOffset, ref phiOffset);

            if (SpecialConfig.Map3DMode == Map3DCameraMode.CameraPosAndAngle)
            {
                SpecialConfig.Map3DCameraYaw += thetaOffset;
                SpecialConfig.Map3DCameraPitch += phiOffset;
                return true;
            }

            (double newX, double newY, double newZ) =
                MoreMath.OffsetSphericallyAboutPivot(
                    SpecialConfig.Map3DFocusX, SpecialConfig.Map3DFocusY, SpecialConfig.Map3DFocusZ,
                    radiusOffset, thetaOffset, phiOffset,
                    SpecialConfig.Map3DCameraX, SpecialConfig.Map3DCameraY, SpecialConfig.Map3DCameraZ);

            SpecialConfig.Map3DFocusX = (float)newX;
            SpecialConfig.Map3DFocusY = (float)newY;
            SpecialConfig.Map3DFocusZ = (float)newZ;

            return true;
        }

        public static bool TranslateMapCameraFocus(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            MapUtilities.MaybeChangeMapCameraMode();
            List<PositionAngle> posAngles = new List<PositionAngle> { PositionAngle.MapCamera, PositionAngle.MapFocus };
            return ChangeValues(posAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }
    }
}
