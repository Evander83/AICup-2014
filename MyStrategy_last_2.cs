using System;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk
{
    public sealed class MyStrategy : IStrategy
    {
        static Player OP;
        static Player MP;
        static double RinkMiddleHeight;
        static double RinkMiddleWidth;

        public double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2.0 * Math.PI;
            while (angle < -Math.PI) angle += 2.0 * Math.PI;
            return angle;
        }

        public double GetAngleBetween(double x, double y, double x1, double y1, double angle)
        {
            double absoluteAngleTo = Math.Atan2(y1 - y, x1 - x);
            double relativeAngleTo = absoluteAngleTo - angle;
            return NormalizeAngle(relativeAngleTo);
        }

        public void GoTo(Unit self, Unit target, double back, Game game, Move move)
        {
            GoTo(self, target.X, target.Y, target.SpeedX, target.SpeedY, back, game, move);
        }

        public void GoTo(Unit self, double tX, double tY, double tSpeedX, double tSpeedY, double back, Game game, Move move)
        {
            double Rx = self.X - tX;
            double Ry = self.Y - tY;
            double Vx = self.SpeedX - tSpeedX;
            double Vy = self.SpeedY - tSpeedY;

            double t = (Vx * Vx + Vy * Vy) / (Vx * Vx + Vy * Vy + Rx * Rx + Ry * Ry);
            double Ax = -Rx * (1 - t) - Vx * t;
            double Ay = -Ry * (1 - t) - Vy * t;

            move.SpeedUp = Math.Cos(GetAngleBetween(0, 0, Ax, Ay, self.Angle))
                * Math.Sqrt(Ax * Ax + Ay * Ay);
            
            if (move.SpeedUp > 0) move.SpeedUp /= game.HockeyistSpeedUpFactor;
            else move.SpeedUp /= game.HockeyistSpeedDownFactor;
            
            double Frw = GetAngleBetween(0, 0, Ax, Ay, self.Angle);
            double Bck = GetAngleBetween(0, 0, -Ax, -Ay, self.Angle);

            move.Turn = (self.GetDistanceTo(tX, tY) > back || Math.Abs(Frw) < Math.Abs(Bck)) ? Frw : Bck;
        }

        public bool CanShot(Unit self, Puck Puck, double Speed, double Sector,
            double TX, double TY, double GoalieMaxSpeed)
        {
            return CanShot(self.Angle, self.Radius, Puck.X, Puck.Y,
                Puck.Radius, Speed, Sector, TX, TY, GoalieMaxSpeed);
        }

        public bool CanShot(double Angle, double Radius, double PuckX, double PuckY,
            double PuckRadius, double Speed, double Sector,
            double TX, double TY, double GoalieMaxSpeed)
        {
            if (Math.Abs(GetAngleBetween(PuckX, PuckY, TX, TY, Angle)) > Sector) return false;

            double cos = (TX - PuckX) / Math.Sqrt((PuckX - TX) * (PuckX - TX) + (PuckY - TY) * (PuckY - TY));
            double sin = (TY - PuckY) / Math.Sqrt((PuckX - TX) * (PuckX - TX) + (PuckY - TY) * (PuckY - TY));

            double Vx = Speed * cos;
            double Vy = (Math.Abs(Speed * sin) > GoalieMaxSpeed) ?
                Speed * sin - Math.Sign(sin) * GoalieMaxSpeed : 0.0;

            double x0 = Math.Abs(PuckX - TX);
            if (PuckY > OP.NetBottom - Radius || PuckY < OP.NetTop + Radius)
                x0 *= (Math.Abs(OP.NetBottom - OP.NetTop - 2 * Radius)
                    / Math.Abs(TY - PuckY));
            double y0 = 0;

            double tmin = -x0 * Vx / (Vx * Vx + Vy * Vy);
            double xmin = x0 + Vx * tmin;
            double ymin = y0 + Vy * tmin;
            double rmin = Math.Sqrt(xmin * xmin + ymin * ymin);

            if (rmin >= PuckRadius + Radius + 5)
                return true;
            return false;
        }


        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            OP = world.GetOpponentPlayer();
            MP = world.GetMyPlayer();
            RinkMiddleHeight = (game.RinkTop + game.RinkBottom) / 2;
            RinkMiddleWidth = (game.RinkLeft + game.RinkRight) / 2;
            double TX;
            double TY;

            // 0. Таблица ударных позиций
            /*
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter("Speed.txt"))
            {
                double[,] xy = new double[200, 200];
                for (double speed = 0.0; speed <= 70.0; speed += 1.0)
                    for (double y = game.RinkTop; y <= game.RinkBottom; y += 10.0)
                        for (double x = game.RinkLeft; x <= game.RinkRight; x += 10.0)
                        {
                            TX = OP.NetFront;
                            TY = (y <= RinkMiddleHeight) ? OP.NetBottom - 9 : OP.NetTop + 9;
                            if (CanShot(GetAngleBetween(x, y, TX, TY, 0), self.Radius, x, y, world.Puck.Radius,
                                speed, Math.PI / 3, OP.NetFront, (y >= RinkMiddleHeight) ? OP.NetBottom : OP.NetTop, game.GoalieMaxSpeed))
                                if (xy[(int)(x / 10), (int)(y / 10)] == 0)
                                    xy[(int)(x / 10), (int)(y / 10)] = speed;
                        }

                for (double y = game.RinkTop; y <= game.RinkBottom; y += 10.0)
                {
                    for (double x = game.RinkLeft; x <= game.RinkRight; x += 10.0)
                        sw.Write("{0} ", ((int)(xy[(int)(x / 10), (int)(y / 10)])).ToString("D2"));
                    sw.WriteLine();
                }
                return;
            }
            */


            // 1. Завершение удара
            // если идет замах более 13 тиков, то ударить
            if (self.LastAction == ActionType.Swing && world.Tick - self.LastActionTick >= 13.0)
            {
                move.Action = ActionType.Strike;
                return;
            }

            // 2. Назначение второго вратаря
            // TX, TY - координаты позиции второго вратаря
            TX = (MP.NetFront < RinkMiddleWidth) ? MP.NetFront + 80 : MP.NetFront - 80;
            TY = RinkMiddleHeight;

            // проверить является ли хоккеист ближайшим к позиции вратаря
            bool nearest = true;
            foreach (Hockeyist h in world.Hockeyists)
                if (self.GetDistanceTo(TX, TY) > h.GetDistanceTo(TX, TY) &&
                    h.PlayerId == MP.Id &&
                    h.Type != HockeyistType.Goalie)
                    nearest = false;

            // ближайщий к воротам не владеющий шайбой становится вторым вратарем
            if (world.Puck.OwnerPlayerId == MP.Id && world.Puck.OwnerHockeyistId != self.Id ||
                world.Puck.OwnerPlayerId != MP.Id && nearest)
            {
                if (self.GetDistanceTo(world.Puck) < game.StickLength &&
                    Math.Abs(self.GetAngleTo(world.Puck)) <= game.StickSector)
                {
                    if (world.Puck.SpeedX * world.Puck.SpeedX +
                        world.Puck.SpeedY * world.Puck.SpeedY < 12.0 * 12.0 &&
                        world.Puck.OwnerHockeyistId == -1)
                    {
                        move.Action = ActionType.TakePuck;
                        return;
                    }
                    else
                    {
                        move.Action = ActionType.Strike;
                        return;
                    }
                }

                if (self.GetDistanceTo(world.Puck) < 2.6 * game.StickLength && 
                    self.GetDistanceTo(TX, TY) < 1.2 * game.StickLength &&
                    world.Puck.OwnerHockeyistId != MP.Id)
                {
                    move.SpeedUp = 1.0;
                    move.Turn = self.GetAngleTo(world.Puck);
                    return;
                }
                else
                    if (self.GetDistanceTo(world.Puck) < 5 * game.StickLength && self.GetDistanceTo(TX, TY) < 30)
                    {
                        move.SpeedUp = 0.0;
                        move.Turn = self.GetAngleTo(world.Puck);
                    }
                    else
                        GoTo(self, TX, TY, 0, 0, 200, game, move);


                // выбрать первого попавшегося вражеского игрока и бить клюшкой
                foreach (Hockeyist h in world.Hockeyists)
                    if (h.IsTeammate == false &&
                        h.Type != HockeyistType.Goalie &&
                        h.State == HockeyistState.Active)
                        if (self.GetDistanceTo(h) < game.StickLength &&
                            Math.Abs(self.GetAngleTo(h)) <= game.StickSector)
                        {
                            move.Action = ActionType.Strike;
                            return;
                        }

                // вратарь ничего не делает
                return;
            }

            // 3. Отбор шайбы
            // наша команда не владеет шайбой
            if (world.Puck.OwnerPlayerId != MP.Id)
            {
                // двигаться к шайбе
                GoTo(self, world.Puck, 0, game, move);

                // если шайбой никто не владеет и она в пределах клюшки, подобрать
                if (world.Puck.OwnerHockeyistId == -1 &&
                    self.GetDistanceTo(world.Puck) < game.StickLength &&
                    Math.Abs(self.GetAngleTo(world.Puck)) <= game.StickSector)
                {
                    move.Action = ActionType.TakePuck;
                    return;
                }

                // если вражеский хоккеист в пределах клюшки, ударить его
                foreach (Hockeyist h in world.Hockeyists)
                    if (h.Id == world.Puck.OwnerHockeyistId)
                    {
                        // двигаться к владельцу шайбы
                        GoTo(self, h, 0, game, move);
                        if (self.GetDistanceTo(h) < game.StickLength &&
                            Math.Abs(self.GetAngleTo(h)) <= game.StickSector)
                        {
                            move.Action = ActionType.Strike;
                            return;
                        }
                    }

                // команда без шайбы, а хоккеист ничего не делает
                return;
            }

            // 4. Атака 
            // наша команда владеет шайбой
            else
                // если хоккеист владеет шайбой
                if (world.Puck.OwnerHockeyistId == self.Id)
                {
                    TX = OP.NetFront;
                    TY = (world.Puck.Y <= RinkMiddleHeight) ? OP.NetBottom - 9 : OP.NetTop + 9;

                    // если можно забить страйком
                    if (CanShot(self, world.Puck, 20.0, Math.PI / 360.0, TX, TY, game.GoalieMaxSpeed))
                    {
                        move.Action = ActionType.Swing;
                        return;
                    }

                    // если можно забить пасом
                    if (CanShot(self, world.Puck, 15.0, Math.PI / 3.0, TX, TY, game.GoalieMaxSpeed))
                    {
                        move.PassPower = 1.0;
                        move.PassAngle = GetAngleBetween(world.Puck.X, world.Puck.Y,
                            TX, TY, self.Angle);
                        move.Action = ActionType.Pass;
                        return;
                    }

                    // ВЫБОР НАПРАВЛЕНИЯ
                    // нужно выбрать и ехать к ударной позиции
                    TX = RinkMiddleWidth;
                    TY = RinkMiddleHeight;
                    double dX = 110.0;
                    double X1 = 367.5;
                    double Y1 = 107.0;
                    double X2 = 267.5;
                    double Y2 = 207.0;
                    double t;
                    if (world.Puck.Y <= RinkMiddleHeight && OP.NetFront < RinkMiddleWidth)
                    {
                        if (world.Puck.X - dX >= game.RinkLeft + X1)
                        {
                            TX = world.Puck.X - dX;
                            TY = game.RinkTop + Y1;
                        }
                        else
                            if (world.Puck.X - dX >= game.RinkLeft + X2)
                            {
                                TX = world.Puck.X - dX;
                                t = (TX - game.RinkLeft - X2) / (X1 - X2);
                                TY = game.RinkTop + Y1 * t + Y2 * (1 - t);
                            }
                            else
                            {
                                TX = game.RinkLeft + X2;
                                TY = game.RinkTop + Y2;
                            }
                    }
                    if (world.Puck.Y <= RinkMiddleHeight && OP.NetFront >= RinkMiddleWidth)
                    {
                        if (world.Puck.X + dX <= game.RinkRight - X1)
                        {
                            TX = world.Puck.X + dX;
                            TY = game.RinkTop + Y1;
                        }
                        else
                            if (world.Puck.X + dX <= game.RinkRight - X2)
                            {
                                TX = world.Puck.X + dX;
                                t = (game.RinkRight - TX - X2) / (X1 - X2);
                                TY = game.RinkTop + Y1 * t + Y2 * (1 - t);
                            }
                            else
                            {
                                TX = game.RinkRight - X2;
                                TY = game.RinkTop + Y2;
                            }
                    }
                    if (world.Puck.Y > RinkMiddleHeight && OP.NetFront < RinkMiddleWidth)
                    {
                        if (world.Puck.X - dX >= game.RinkLeft + X1)
                        {
                            TX = world.Puck.X - dX;
                            TY = game.RinkBottom - Y1;
                        }
                        else
                            if (world.Puck.X - dX >= game.RinkLeft + X2)
                            {
                                TX = world.Puck.X - dX;
                                t = (TX - game.RinkLeft - X2) / (X1 - X2);
                                TY = game.RinkBottom - Y1 * t - Y2 * (1 - t);
                            }
                            else
                            {
                                TX = game.RinkLeft + X2;
                                TY = game.RinkBottom - Y2;
                            }
                    }
                    if (world.Puck.Y > RinkMiddleHeight && OP.NetFront >= RinkMiddleWidth)
                    {
                        if (world.Puck.X + dX <= game.RinkRight - X1)
                        {
                            TX = world.Puck.X + dX;
                            TY = game.RinkBottom - Y1;
                        }
                        else
                            if (world.Puck.X + dX <= game.RinkRight - X2)
                            {
                                TX = world.Puck.X + dX;
                                t = (game.RinkRight - TX - X2) / (X1 - X2);
                                TY = game.RinkBottom - Y1 * t - Y2 * (1 - t);
                            }
                            else
                            {
                                TX = game.RinkRight - X2;
                                TY = game.RinkBottom - Y2;
                            }
                    }

                    if (world.Puck.X > RinkMiddleWidth && OP.NetFront <= RinkMiddleWidth)
                    {
                        TX = world.Puck.X - dX;
                        Hockeyist hm = null;
                        foreach (Hockeyist h in world.Hockeyists)
                            if (h.IsTeammate == false &&
                                h.Type != HockeyistType.Goalie &&
                                (hm == null || self.GetDistanceTo(hm) > self.GetDistanceTo(h)))
                                hm = h;
                        if (hm.Y <= self.Y)
                            TY = game.RinkBottom - Y1;
                        else
                            TY = game.RinkTop + Y1;
                    }
                    if (world.Puck.X <= RinkMiddleWidth && OP.NetFront > RinkMiddleWidth)
                    {
                        TX = world.Puck.X + dX;
                        Hockeyist hm = null;
                        foreach (Hockeyist h in world.Hockeyists)
                            if (h.IsTeammate == false &&
                                h.Type != HockeyistType.Goalie &&
                                (hm == null || self.GetDistanceTo(hm) > self.GetDistanceTo(h)))
                                hm = h;
                        if (hm.Y <= self.Y)
                            TY = game.RinkBottom - Y1;
                        else
                            TY = game.RinkTop + Y1;
                    }

                    // ПРОДВИЖЕНИЕ
                    // если позиция далеко, ехать дальше
                    if (world.Puck.GetDistanceTo(TX, TY) >= 100.0)
                    {
                        move.SpeedUp = 1.0;
                        move.Action = ActionType.None;
                        move.Turn = self.GetAngleTo(TX, TY);
                    }

                    // около ударной позиции замедлиться, развернуться 
                    // и замахнуться в верхнюю или нижнюю штангу
                    else
                    {
                        TY = (world.Puck.Y >= RinkMiddleHeight) ? OP.NetTop + 9 : OP.NetBottom - 9;
                        move.SpeedUp = 0.0;
                        move.Turn = self.GetAngleTo(OP.NetFront, TY);
                        if (Math.Abs(GetAngleBetween(world.Puck.X, world.Puck.Y,
                            OP.NetFront, TY, self.Angle)) <= Math.PI / 360.0)
                        {
                            move.Action = ActionType.Swing;
                            return;
                        }
                        else
                            move.Action = ActionType.None;
                    }

                    // можно отдать пас
                    foreach (Hockeyist h in world.Hockeyists)
                        if (h.IsTeammate == true &&
                            h.Type != HockeyistType.Goalie &&
                            h.State == HockeyistState.Active)
                            if (Math.Abs(self.GetAngleTo(h)) <= Math.PI / 90.0)
                            {
                                move.PassAngle = self.GetAngleTo(h);
                                move.PassPower = 0.75;
                                move.Action = ActionType.Pass;
                            }


                    // хоккеист владеет шайбой и при этом ничего не делает
                    return;
                }

                // хоккеист без шайбы атакует вражеских хоккеистов
                else
                {
                    // выбрать первого попавшегося вражеского игрока и бить клюшкой
                    foreach (Hockeyist h in world.Hockeyists)
                        if (h.IsTeammate == false &&
                            h.Type != HockeyistType.Goalie &&
                            h.State == HockeyistState.Active)
                        {
                            move.SpeedUp = 1.0;
                            move.Turn = self.GetAngleTo(h);
//                            GoTo(self, h, 0, game, move);

                            if (self.GetDistanceTo(h) < game.StickLength &&
                                Math.Abs(self.GetAngleTo(h)) <= game.StickSector)
                                move.Action = ActionType.Strike;
                            else
                                move.Action = ActionType.TakePuck;
                            return;
                        }

                    // хоккеист без шайбы и при этом ничего не делает
                    return;
                }
        }
    }
}