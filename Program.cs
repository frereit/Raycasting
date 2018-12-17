using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Raycasting
{

    public delegate void UpdateDelegate();
    
    public class Rectangles
    {
        /* Helper class for containing results of Raycasting Threads */
        public Rectangle DestRect, SrcRect;

        public Rectangles(Rectangle destRect, Rectangle srcRect)
        {
            DestRect = destRect;
            SrcRect = srcRect;
        }
    }
    
    internal class Program : Form
    {
        //Constants
        private const double Fov = Math.PI / 3;
        private const int BlockSize = 64;
        private const int ThreadCount = 1;
        
        //Bitmaps
        private Bitmap _map = new Bitmap(@"map.png");
        private readonly Bitmap _texture = new Bitmap(@"texture.png");
        
        //Movement
        private PointF _posd = new PointF(96, 96);
        private double view_angle = 0;
        private HashSet<Keys> _keys = new HashSet<Keys>();
       
        //Drawing related
        private Thread runner;
        private int frames = 0;
        private double fps = 0;
        private Stopwatch frameStopwatch = new Stopwatch();
        private Stopwatch deltaStopwatch = new Stopwatch();

        private Program()
        {
            Text = "Raycasting";
            SetBounds(0, 0, _map.Width, _map.Height);
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            Paint += Draw;
            runner = new Thread(Runner);
            frameStopwatch.Start();
            deltaStopwatch.Start();
            KeyDown += KeyDownHandler;
            KeyUp += KeyUpHandler;
            Closing += StopAll;
            
            runner.Start();
        }

        private void StopAll(object sender, CancelEventArgs e)
        {
            runner.Abort();
        }


        private void Runner()
        {
            Thread.Sleep(100);
            while (true)
            {
                Invoke(new Action(Update));
            }
        }

        private void move(bool forward, bool strafe, float deltat)
        {
            var multDirection = forward ? 1 : -1; /* Move Backward if forward is false by subtracting */
            var multAngle = strafe ? 1 : 0; /* Leave angle as if if not strafing */ 
            
            var newX = _posd.X + multDirection * ((float) Math.Cos(view_angle+(1d/2d*Math.PI*multAngle)) * 2 * deltat);
            var newY = _posd.Y + multDirection * ((float) Math.Sin(view_angle+(1d/2d*Math.PI*multAngle)) * 2 * deltat);
            if (_map.GetPixel((int) newX, (int) newY) != Color.FromArgb(255, 255, 255, 255)) return;
            if (newX < 1 || newY < 1) return;
            _posd.X = newX;
            _posd.Y = newY;
        }

        private void Update()
        {
            
            /* Recalculate FPS every frame */
            if (frameStopwatch.ElapsedMilliseconds > 1000)
            {
                frameStopwatch.Restart();
                frames = 0;
            }
            
            
            var deltat = deltaStopwatch.ElapsedMilliseconds / 10f;
            deltaStopwatch.Restart();
            foreach (var key in _keys)
            {
                
                switch(key)
                {
                    case Keys.Left:
                        view_angle -= 0.05 * deltat;
                        break;
                    case Keys.Right:
                        view_angle += 0.05 * deltat;
                        break;
                    case Keys.M:
                        _map = new Bitmap(@"map.png");
                        break;
                }

                if (new[] {Keys.W, Keys.A, Keys.S, Keys.D}.Contains(key))
                {
                    var forward = key == Keys.W || key == Keys.D;
                    var strafe = key == Keys.A || key == Keys.D;
                    move(forward, strafe, deltat);
                }
                
            }

            fps = frames / (frameStopwatch.ElapsedMilliseconds / 1000d);
            Refresh();
            
        }
        
        private void KeyUpHandler(object sender, KeyEventArgs e)
        {
            _keys.Remove(e.KeyCode);
        }

        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            _keys.Add(e.KeyCode);
        }

        private void Draw(object sender, PaintEventArgs e)
        {
            //Background
            e.Graphics.FillRectangle(Brushes.Cyan, 0, 0,Width, Height/2);
            e.Graphics.FillRectangle(Brushes.Beige, 0, Height/2, Width, Height/2);

            //3D STUFF
            var x = 0;
            for (var angle = view_angle - (Fov / 2d); angle < view_angle + (Fov / 2d); angle += Fov / Width)
            {
                
                //Find Intersection
                var horizontalIntersection = FindHorizontalIntersection(angle);
                var verticalIntersection = FindVerticalIntersection(angle);
                var horizontalDist = GetDistance(_posd, horizontalIntersection);
                var verticalDist = GetDistance(_posd, verticalIntersection);
                var isHorizontalIntersection = horizontalDist < verticalDist && horizontalIntersection.X > 0;
                var intersection = isHorizontalIntersection ? horizontalIntersection : verticalIntersection;
                var distorted_distance = isHorizontalIntersection ? horizontalDist : verticalDist;
                
                //Calculate Distance
                var dist = Math.Cos(view_angle - angle) * distorted_distance;
                var projected_height = 64 / dist * (Width/(2*Math.Tan(Fov/2)));
                
                //Draw
                if (intersection.X > 1 && intersection.X != _map.Width && intersection.Y > 1 &&
                    intersection.Y != _map.Height)
                {
                    var textureIndex = isHorizontalIntersection
                        ? (intersection.X % BlockSize * (_texture.Width / BlockSize))
                        : (intersection.Y % BlockSize * (_texture.Width / BlockSize));
                    e.Graphics.DrawImage(_texture,
                        new Rectangle(x, (int) (Height / 2d - projected_height / 2d), 1, (int) projected_height),
                        new Rectangle(textureIndex, 0, 1, _texture.Height), GraphicsUnit.Pixel);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.Red, new Rectangle(x, (int) (Height / 2d - projected_height / 2d), 1, (int) projected_height));
                }

                x++;
            }
            
            
            //Minimap
            var w = Width * 0.1;
            var h = Height * 0.1;
            e.Graphics.DrawImage(_map, new Rectangle(0, 0, (int) w, (int) h));
            int smallX = (int) (_posd.X * (w / _map.Width));
            int smallY = (int) (_posd.Y * (h / _map.Height));
            e.Graphics.FillEllipse(Brushes.Green, smallX, smallY, 5, 5);

            
            //FPS
            e.Graphics.FillRectangle(Brushes.White, Width-90, 10, 75, 15);
            e.Graphics.DrawString("FPS: " + Math.Round(fps, 2), DefaultFont, Brushes.Black, Width-90, 10);
            frames++;
        }

        private Point FindVerticalIntersection(double angle)
        {
            var A = new Point();
            int Ya, Xa;
            if (Math.Cos(angle) > 0)
            {
                //Facing right
                A.X = (int) (_posd.X / BlockSize) * BlockSize + BlockSize;
                Xa = BlockSize;
            }else if (Math.Cos(angle) < 0)
            {
                //Facing left
                A.X = (int) (_posd.X / BlockSize) * BlockSize -1;
                Xa = -BlockSize;
            }
            else
            {
                return new Point(-1, -1);
            }
            
            Ya = (int) (BlockSize * Math.Tan(angle));
            Ya = Math.Sin(angle) >= 0 ? Math.Abs(Ya) : -Math.Abs(Ya);
            A.Y = (int) (_posd.Y + (A.X - _posd.X) * Math.Tan(angle));

            while (!IsWall(A))
            {
                A.X += Xa;
                A.Y += Ya;

            }

            return A;
        }

        private Point FindHorizontalIntersection(double angle)
        {
            var A = new PointF();
            float Ya, Xa;
            if (Math.Sin(angle) < 0)
            {
                //Facing up
                Ya = -BlockSize;
              
                A.Y = (int) (_posd.Y / BlockSize) * BlockSize;
                A.X = (int) (_posd.X + ((A.Y - _posd.Y)/ Math.Tan(angle)));
                A.Y--;
            }else if (Math.Sin(angle) > 0)
            {
                //Facing down
                Ya = BlockSize;
                
                A.Y = (int) (_posd.Y / BlockSize) * BlockSize + BlockSize;
                A.X = (int) (_posd.X + ((A.Y - _posd.Y)/ Math.Tan(angle)));
            }
            else
            {
                return new Point(-1, -1);
            }
            
            Xa = (float) (BlockSize / Math.Tan(angle));
            Xa = Math.Cos(angle) <= 0 ? -1 * Math.Abs(Xa) : Math.Abs(Xa);
            
            
            while (!IsWall(A))
            {
                A.X += Xa;
                A.Y += Ya;
            }
            
             
            return new Point((int)A.X, (int)A.Y);

        }

        private bool IsWall(PointF point)
        {
            return IsWall(new Point((int) point.X, (int) point.Y));
        }

        private bool IsWall(Point point)
        {
            if (point.X < 0 || point.X >= _map.Width) return true;
            if (point.Y < 0 || point.Y >= _map.Height) return true;
            return _map.GetPixel(point.X, point.Y) != Color.FromArgb(255, 255, 255, 255);
        }

        private ArrayList Raycast(double minAngle, double maxAngle, double stepSize, double xstart)
        {
            return null;
        }


        private static double GetDistance(PointF a, PointF b)
        {
            var deltaX = b.X - a.X;
            var deltaY = b.Y - a.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public static void Main(string[] args)
        {
            Application.Run(new Program());
        }
    }
}