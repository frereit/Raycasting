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
        private Bitmap _map = new Bitmap(@"map.png");
        private readonly Bitmap _texture = new Bitmap(@"texture.png");
        private readonly double _fov = Math.PI/3;
        private Point _pos = new Point(50, 50);
        private PointF _posd = new PointF(50, 50);
        private double view_angle = 0;
        private HashSet<Keys> _keys = new HashSet<Keys>();
        private const int ThreadCount = 16;
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
            
            var newX = _posd.X + multDirection * ((float) Math.Cos(view_angle+(1d/2d*Math.PI*multAngle)) * deltat);
            var newY = _posd.Y + multDirection * ((float) Math.Sin(view_angle+(1d/2d*Math.PI*multAngle)) * deltat);
            if (_map.GetPixel((int) newX, (int) newY) != Color.FromArgb(255, 255, 255, 255)) return;
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
            _pos.X = (int) _posd.X % Width;
            _pos.Y = (int) _posd.Y % Height;
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
            
            var rects = ArrayList.Synchronized(new ArrayList());
            var count = 0;
            for (var i = 0; i < ThreadCount; i++)
            {
                Interlocked.Increment(ref count);
                var i1 = i;
                ThreadPool.QueueUserWorkItem((object stateInfo) =>
                {
                    var result = Raycast(view_angle - _fov / 2 + i1 * (_fov / ThreadCount),
                        view_angle - _fov / 2 + (i1 + 1) * (_fov / ThreadCount), _fov / Width,
                        Width / ThreadCount * i1);
                    rects.AddRange(result);
                    Interlocked.Decrement(ref count);
                });
            }
            while(count > 0){}

            foreach (var rect in rects)
            {
                var casted = (Rectangles) rect;
                e.Graphics.DrawImage(_texture, casted.DestRect, casted.SrcRect, GraphicsUnit.Pixel);
            }
            
            //Minimap
            
            e.Graphics.DrawImage(_map, new Rectangle(0, 0, 100, 100));
            var smallX = (int) (_pos.X * (100.0 / _map.Width));
            var smallY = (int) (_pos.Y * (100.0 / _map.Height));
            e.Graphics.FillEllipse(Brushes.Green, smallX, smallY, 5, 5);
            
            e.Graphics.DrawString("FPS: " + Math.Round(fps, 2), DefaultFont, Brushes.Black, Width-90, 10);
            frames++;
        }

        private ArrayList Raycast(double minAngle, double maxAngle, double stepSize, double xstart)
        {
            var rects = new ArrayList();
            //Rendering
            var x = xstart;

            for (var angle = minAngle; angle < maxAngle; angle += stepSize)
            {
                var cx = (double) _posd.X;
                var cy = (double) _posd.Y;

                /* Search Wall */
                while (_map.GetPixel((int) cx, (int) cy) == Color.FromArgb(255, 255, 255, 255))
                {
                    cx += Math.Cos(angle);
                    cy += Math.Sin(angle);
                }

                /* Distance Calculation */
                var beta = angle - view_angle;
                var distorted = Math.Sqrt((cx - _posd.X) * (cx - _posd.X) + (cy - _posd.Y) * (cy - _posd.Y));
                var dist = Math.Cos(beta) * distorted;
                var height = 32 / dist * ((Width/2) /Math.Tan(_fov/2));
                
                /* Texture Calculation */
                var pixCol = _map.GetPixel((int) cx, (int) cy);
                var red = (int) pixCol.R;
                var destRect = new Rectangle((int) x, (int) (Height / 2 - height / 2), 1, (int) height);
                var srcRect = new Rectangle(red, 0, 1, _texture.Height);
                rects.Add(new Rectangles(destRect, srcRect));
                x++;
            }
            return rects;
        }

        public static void Main(string[] args)
        {
            Application.Run(new Program());
        }
    }
}