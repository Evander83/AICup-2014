using System;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk
{
    public sealed class MyStrategy : IStrategy
    {
        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            move.Action = ActionType.None;
            move.PassAngle = 0.0;
            move.PassPower = 0.0;
            move.SpeedUp = 0.0;
            move.TeammateIndex = -1;
            move.Turn = 0.0;        
        }
    }
}