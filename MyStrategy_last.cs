using System;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk
{
    public sealed class MyStrategy : IStrategy
    {
        public double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2.0D * Math.PI;
            while (angle < -Math.PI) angle += 2.0D * Math.PI;
            return angle;
        }

        public double GetAngleBetween(double x, double y, double x1, double y1, double angle)
        {
            double absoluteAngleTo = Math.Atan2(y1 - y, x1 - x);
            double relativeAngleTo = absoluteAngleTo - angle;

            return NormalizeAngle(relativeAngleTo);
        }

        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            Player OP = world.GetOpponentPlayer();
            Player MP = world.GetMyPlayer();
            double RinkMiddleHeight = (game.RinkTop + game.RinkBottom) / 2;
            double RinkMiddleWidth = (game.RinkLeft + game.RinkRight) / 2;
            double TargetX;
            double TargetY;

            // 1. Завершение удара
            // если идет замах более 13 тиков, то ударить
            if (self.LastAction == ActionType.Swing && world.Tick - self.LastActionTick >= 10)
            {
                move.Action = ActionType.Strike;
                return;
            }

            // 2. Назначение второго вратаря
            // NX, NY - координаты позиции второго вратаря
            double NX = (MP.NetFront < RinkMiddleWidth) ? MP.NetFront + 75 : MP.NetFront - 75;
            double NY = RinkMiddleHeight;

            // проверить является ли хоккеист ближайшим к позиции вратаря
            bool nearest = true;
            foreach (Hockeyist h in world.Hockeyists)
                if (self.GetDistanceTo(NX, NY) > h.GetDistanceTo(NX, NY) &&
                    h.PlayerId == MP.Id &&
                    h.Type != HockeyistType.Goalie)
                    nearest = false;

            // ближайщий к воротам не владеющий шайбой становится вторым вратарем
            if (world.Puck.OwnerPlayerId == MP.Id && world.Puck.OwnerHockeyistId != self.Id ||
                world.Puck.OwnerPlayerId != MP.Id && nearest)
            {
                // двигаться к позиции второго вратаря
                move.SpeedUp = 1.0D;
                move.Turn = self.GetAngleTo(NX, NY);
                move.Action = ActionType.TakePuck;

                // приблизившись к позиции тормозить и разворачиваться
                if (self.GetDistanceTo(NX, NY) < 270)
                {
                    move.SpeedUp = -1.0D;
                    move.Turn = NormalizeAngle(self.GetAngleTo(NX, NY) + Math.PI);
                    move.Action = ActionType.TakePuck;
                }

                // полностью остановиться в небольшом радиусе от позиции
                if (self.GetDistanceTo(NX, NY) < 70)
                {
                    move.SpeedUp = 0.0D;
                    move.Turn = self.GetAngleTo(world.Puck);
                    if (self.GetDistanceTo(world.Puck) < game.StickLength)
                    {
                        if (world.Puck.SpeedX * world.Puck.SpeedX +
                            world.Puck.SpeedY * world.Puck.SpeedY < 12 * 12 &&
                            world.Puck.OwnerHockeyistId == -1)
                            move.Action = ActionType.TakePuck;
                        else
                            move.Action = ActionType.Strike;
                    }
                }
                return;
            }

            // 3. Отбор шайбы
            // наша команда не владеет шайбой
            if (world.Puck.OwnerPlayerId != MP.Id)
            {
                // двигаться к шайбе
                move.SpeedUp = 1.0D;
                move.Turn = self.GetAngleTo(world.Puck);

                // если шайбой никто не владеет и она в пределах клюшки, подобрать
                if (world.Puck.OwnerHockeyistId == -1 &&
                    self.GetDistanceTo(world.Puck) < game.StickLength)
                {
                    move.Action = ActionType.TakePuck;
                    return;
                }

                // если вражеский хоккеист в пределах клюшки, ударить его
                foreach (Hockeyist h in world.Hockeyists)
                    if (h.Id == world.Puck.OwnerHockeyistId)
                        if (self.GetDistanceTo(h) < game.StickLength &&
                            Math.Abs(self.GetAngleTo(h)) <= game.StickSector)
                        {
                            move.Action = ActionType.Strike;
                            return;
                        }

                return;
            }

            // 4. Атака 
            // наша команда владеет шайбой
            else
            {
                // если хоккеист владеет шайбой
                if (world.Puck.OwnerHockeyistId == self.Id)
                {
                    // если можно забить страйком
                    {
                        double TX = OP.NetFront;
                        double TY = (world.Puck.Y <= RinkMiddleHeight) ?
                            OP.NetBottom - 5 : OP.NetTop + 5;

                        double cos = (TX - world.Puck.X) / world.Puck.GetDistanceTo(TX, TY);
                        double sin = (TY - world.Puck.Y) / world.Puck.GetDistanceTo(TX, TY);

                        double Vx = 20.0 * cos;
                        double Vy = (Math.Abs(20.0 * sin) > game.GoalieMaxSpeed) ?
                            20.0 * sin - Math.Sign(sin) * game.GoalieMaxSpeed : 0.0;

                        double x0 = Math.Abs(world.Puck.X - TX);
                        if (world.Puck.Y > OP.NetBottom || world.Puck.Y < OP.NetTop)
                            x0 *= (Math.Abs(OP.NetBottom - OP.NetTop - 2 * self.Radius)
                                / Math.Abs(TY - world.Puck.Y));
                        double y0 = 0;

                        double tmin = -x0 * Vx / (Vx * Vx + Vy * Vy);
                        double xmin = x0 + Vx * tmin;
                        double ymin = y0 + Vy * tmin;
                        double rmin = Math.Sqrt(xmin * xmin + ymin * ymin);

                        // если можно забить страйком
                        if (rmin >= world.Puck.Radius + self.Radius + 5)
                            if (Math.Abs(GetAngleBetween(world.Puck.X, world.Puck.Y,
                                TX, TY, self.Angle)) <= Math.PI / 360.0D)
                            {
                                move.Action = ActionType.Swing;
                                return;
                            }
                    }

                    // если можно забить пасом
                    {
                        double TX = OP.NetFront;
                        double TY = (world.Puck.Y <= RinkMiddleHeight) ?
                            OP.NetBottom - 5 : OP.NetTop + 5;

                        double cos = (TX - world.Puck.X) / world.Puck.GetDistanceTo(TX, TY);
                        double sin = (TY - world.Puck.Y) / world.Puck.GetDistanceTo(TX, TY);

                        double Vx = 15.0 * cos;
                        double Vy = (Math.Abs(15.0 * sin) > game.GoalieMaxSpeed) ?
                            15.0 * sin - Math.Sign(sin) * game.GoalieMaxSpeed : 0.0;

                        double x0 = Math.Abs(world.Puck.X - TX);
                        if (world.Puck.Y > OP.NetBottom || world.Puck.Y < OP.NetTop)
                            x0 *= (Math.Abs(OP.NetBottom - OP.NetTop - 2 * self.Radius)
                                / Math.Abs(TY - world.Puck.Y));
                        double y0 = 0;

                        double tmin = -x0 * Vx / (Vx * Vx + Vy * Vy);
                        double xmin = x0 + Vx * tmin;
                        double ymin = y0 + Vy * tmin;
                        double rmin = Math.Sqrt(xmin * xmin + ymin * ymin);

                        // если можно забить пасом
                        if (rmin >= world.Puck.Radius + self.Radius + 5)
                            if (Math.Abs(GetAngleBetween(world.Puck.X, world.Puck.Y,
                                TX, TY, self.Angle)) <= Math.PI / 3.0D)
                            {
                                move.PassAngle = GetAngleBetween(world.Puck.X, world.Puck.Y,
                                    TX, TY, self.Angle);
                                move.PassPower = 1.0D;
                                move.Action = ActionType.Pass;
                                return;
                            }
                    }

                    // нужно выбрать и ехать к ударной позиции
                    double[,] XY = new double[4, 2] {
                    { 0.25, 0.30 }, { 0.75, 0.30 }, 
                    { 0.25, 0.70 }, { 0.75, 0.70 }
                    };

                    TargetX = RinkMiddleWidth;
                    TargetY = RinkMiddleHeight;
                    if (self.Y <= RinkMiddleHeight && OP.NetFront < RinkMiddleWidth)
                    {
                        TargetX = game.RinkRight * XY[0, 0] + game.RinkLeft * (1 - XY[0, 0]);
                        TargetY = game.RinkBottom * XY[0, 1] + game.RinkTop * (1 - XY[0, 1]);
                    }
                    if (self.Y <= RinkMiddleHeight && OP.NetFront >= RinkMiddleWidth)
                    {
                        TargetX = game.RinkRight * XY[1, 0] + game.RinkLeft * (1 - XY[1, 0]);
                        TargetY = game.RinkBottom * XY[1, 1] + game.RinkTop * (1 - XY[1, 1]);
                    }
                    if (self.Y > RinkMiddleHeight && OP.NetFront < RinkMiddleWidth)
                    {
                        TargetX = game.RinkRight * XY[2, 0] + game.RinkLeft * (1 - XY[2, 0]);
                        TargetY = game.RinkBottom * XY[2, 1] + game.RinkTop * (1 - XY[2, 1]);
                    }
                    if (self.Y > RinkMiddleHeight && OP.NetFront >= RinkMiddleWidth)
                    {
                        TargetX = game.RinkRight * XY[3, 0] + game.RinkLeft * (1 - XY[3, 0]);
                        TargetY = game.RinkBottom * XY[3, 1] + game.RinkTop * (1 - XY[3, 1]);
                    }

                    // если позиция далеко, ехать дальше
                    if (self.GetDistanceTo(TargetX, TargetY) >= 100)
                    {
                        move.SpeedUp = 1.0D;
                        move.Action = ActionType.None;
                        move.Turn = self.GetAngleTo(TargetX, TargetY);

                        if (self.X < RinkMiddleWidth && OP.NetFront >= RinkMiddleWidth ||
                            self.X >= RinkMiddleWidth && OP.NetFront <= RinkMiddleWidth)
                            move.Turn = NormalizeAngle(self.GetAngleTo(TargetX, TargetY) + Math.PI / 6 * ((self.Y < RinkMiddleHeight) ? 1 : -1));

                        return;
                    }

                    // около ударной позиции замедлиться, развернуться 
                    // и замахнуться в верхнюю или нижнюю штангу
                    if (self.GetDistanceTo(TargetX, TargetY) < 100)
                    {
                        move.SpeedUp = 0.1D;
                        if (self.Y >= RinkMiddleHeight)
                            move.Turn = self.GetAngleTo(OP.NetFront, OP.NetTop + 5);
                        if (self.Y < RinkMiddleHeight)
                            move.Turn = self.GetAngleTo(OP.NetFront, OP.NetBottom - 5);

                        if (self.Y >= RinkMiddleHeight)
                        {
                            if (Math.Abs(GetAngleBetween(world.Puck.X, world.Puck.Y,
                                OP.NetFront, OP.NetTop + 5, self.Angle)) <= Math.PI / 360.0D)
                                move.Action = ActionType.Swing;
                            else
                                move.Action = ActionType.None;
                            return;
                        }

                        if (self.Y < RinkMiddleHeight)
                        {
                            if (Math.Abs(GetAngleBetween(world.Puck.X, world.Puck.Y,
                                OP.NetFront, OP.NetBottom - 5, self.Angle)) <= Math.PI / 360.0D)
                                move.Action = ActionType.Swing;
                            else
                                move.Action = ActionType.None;
                            return;
                        }
                    }
                }

                // хоккеист без шайбы атакует вражеских хоккеистов
                else
                {
                    // выбрать ближайщего вражеского игрока и бить клюшкой
                    foreach (Hockeyist h in world.Hockeyists)
                        if (h.IsTeammate == false &&
                            h.Type != HockeyistType.Goalie &&
                            h.State == HockeyistState.Active)
                        {
                            move.SpeedUp = 1.0D;
                            move.Turn = self.GetAngleTo(h);
                            if (self.GetDistanceTo(h) < game.StickLength)
                                move.Action = ActionType.Strike;
                            else
                                move.Action = ActionType.None;
                            return;
                        }
                }
            }
        }
    }
}