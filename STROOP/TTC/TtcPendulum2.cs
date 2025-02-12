﻿using STROOP.Structs;
using STROOP.Structs.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace STROOP.Ttc
{
    /** A pendulum is the pendulum that swings back and forth.
      *  
      *  A pendulum at rest will call RNG to determine how long
      *  it should wait for and how fast it should accelerates
      *  during the next swing. After it's waited the allotted time,
      *  it swings with that acceleration. Once it crosses strictly
      *  past the vertical (i.e. angle 0), the pendulum decelerates
      *  by that same acceleration until it comes to a stop.
      */
    public class TtcPendulum2 : TtcObject
    {

        public int _accelerationDirection;
        public int _angle;
        public int _angularVelocity;
        public int _accelerationMagnitude;
        public int _waitingTimer;

        public TtcPendulum2(TtcRng rng, uint address) :
            this(
                rng: rng,
                accelerationDirection: (int)Config.Stream.GetSingle(address + 0xF4),
                angle: (int)Config.Stream.GetSingle(address + 0xF8),
                angularVelocity: (int)Config.Stream.GetSingle(address + 0xFC),
                accelerationMagnitude: (int)Config.Stream.GetSingle(address + 0x100),
                waitingTimer: Config.Stream.GetInt32(address + 0x104))
        {
        }

        public TtcPendulum2(TtcRng rng) :
            this(rng, 0, 6500, 0, 0, 0)
        {
        }

        public TtcPendulum2(TtcRng rng, int accelerationDirection, int angle,
            int angularVelocity, int accelerationMagnitude, int waitingTimer) : base(rng)
        {
            _accelerationDirection = accelerationDirection;
            _angle = angle;
            _angularVelocity = angularVelocity;
            _accelerationMagnitude = accelerationMagnitude;
            _waitingTimer = waitingTimer;
        }

        public override void Update()
        {

            if (_waitingTimer > 0)
            { //waiting
                _waitingTimer--;
            }
            else
            { //swinging

                if (_accelerationMagnitude == 0)
                { //give initial acceleration on start
                    _accelerationMagnitude = 13;
                }

                if (_angle > 0) _accelerationDirection = -1;
                else if (_angle < 0) _accelerationDirection = 1;

                _angularVelocity = _angularVelocity + _accelerationDirection * _accelerationMagnitude;
                _angle = _angle + _angularVelocity;

                if (_angularVelocity == 0)
                { //reached peak of swing
                    _accelerationMagnitude = _accelerationMagnitude == 13 ? 42 : 13;
                }
            }

        }

        public override string ToString()
        {
            return _id + OPENER + _accelerationDirection + SEPARATOR +
                      _angle + SEPARATOR +
                      _angularVelocity + SEPARATOR +
                      _accelerationMagnitude + SEPARATOR +
                      _waitingTimer + CLOSER;
        }

        public override List<object> GetFields()
        {
            return new List<object>()
            {
                _accelerationDirection, _angle, _angularVelocity, _accelerationMagnitude, _waitingTimer
            };
        }

        public int GetAmplitude()
        {
            return (int)WatchVariableSpecialUtilities.GetPendulumAmplitude(
                _accelerationDirection, _accelerationMagnitude, _angularVelocity, _angle);
        }

        public int? GetSwingIndex()
        {
            return TableConfig.PendulumSwings.GetPendulumSwingIndex(GetAmplitude());
        }

        public string GetSwingIndexExtended()
        {
            return TableConfig.PendulumSwings.GetPendulumSwingIndexExtended(GetAmplitude());
        }

        public int GetCountdown()
        {
            return WatchVariableSpecialUtilities.GetPendulumCountdown(
                _accelerationDirection, _accelerationMagnitude, _angularVelocity, _angle, _waitingTimer);
        }

        public override void ApplyToAddress(uint address)
        {
            Config.Stream.SetValue((float)_accelerationDirection, address + 0xF4);
            Config.Stream.SetValue((float)_angle, address + 0xF8);
            Config.Stream.SetValue((float)_angularVelocity, address + 0xFC);
            Config.Stream.SetValue((float)_accelerationMagnitude, address + 0x100);
            Config.Stream.SetValue(_waitingTimer, address + 0x104);
        }

        public override TtcObject Clone(TtcRng rng)
        {
            return new TtcPendulum2(rng, _accelerationDirection, _angle, _angularVelocity, _accelerationMagnitude, _waitingTimer);
        }
    }

}
