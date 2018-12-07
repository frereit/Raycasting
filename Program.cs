using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Raycasting
{
    internal class Program : Form
    {
        private readonly Bitmap _map = new Bitmap(@"map.png");
        private readonly double _fov = Math.PI/3;
        private Point _pos = new Point(50, 50);
        private PointF _posd = new PointF(50, 50);
        private double view_angle = 0;
        private HashSet<Keys> _keys = new HashSet<Keys>();
        

        private Program()
        {
            Text = "Raycasting";
            SetBounds(0, 0, _map.Width, _map.Height);
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            Paint += Draw;
            var timer = new Timer {Interval = 1};
            timer.Tick += Update;
            timer.Enabled = true;
            timer.Start();
            KeyDown += KeyDownHandler;
            KeyUp += KeyUpHandler;
        }

        private void move(float newX, float newY)
        {
            if (_map.GetPixel((int) newX, (int) newY) != Color.FromArgb(255, 255, 255, 255)) return;
            _posd.X = newX;
            _posd.Y = newY;
        }
        
        private void Update(object sender, EventArgs e)
        {
            _pos.X = (int) _posd.X % Width;
            _pos.Y = (int) _posd.Y % Height;
            foreach (var key in _keys)
            {
                switch(key)
                {
                    case Keys.Left:
                        view_angle -= 0.05;
                        break;
                    case Keys.Right:
                        view_angle += 0.05;
                        break;
                    case Keys.W:
                        var newWx = _posd.X + (float) Math.Cos(view_angle);
                        var newWy = _posd.Y + (float) Math.Sin(view_angle);
                        move(newWx, newWy);
                        break;
                    case Keys.S:
                        var newSx = _posd.X - (float) Math.Cos(view_angle);
                        var newSy = _posd.Y - (float) Math.Sin(view_angle);
                        move(newSx,newSy);
                        break;
                        
                }
            }
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
            
            var x = 0;

            for (var angle = view_angle - _fov / 2; angle < view_angle + _fov / 2; angle += _fov / Width)
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
                var c = Color.FromArgb((int) (dist / Width * 255),(int) (dist / Width * 255),(int) (dist / Width * 255));
                e.Graphics.DrawLine(new Pen(c), x, (int) (Height / 2 - height / 2), x, (int) (Height / 2 + height / 2));

                x++;
            }

            
            //Minimap
            e.Graphics.DrawImage(_map, new Rectangle(0, 0, 100, 100));
            var smallX = (int) (_pos.X * (100.0 / _map.Width));
            var smallY = (int) (_pos.Y * (100.0 / _map.Height));
            e.Graphics.FillEllipse(Brushes.Green, smallX, smallY, 5, 5);
            
        }

        public static void Main(string[] args)
        {
            Application.Run(new Program());
        }
    }
}