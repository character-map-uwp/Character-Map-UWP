using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Core
{
    public class SVGPathReciever : ICanvasPathReceiver
    {
        private StringBuilder _builder { get; }

        public SVGPathReciever()
        {
            _builder = new StringBuilder();
        }

        public void BeginFigure(Vector2 startPoint, CanvasFigureFill figureFill)
        {
            _builder.AppendFormat("M {0} {1} ", startPoint.X, startPoint.Y);
        }

        public void AddArc(Vector2 endPoint, float radiusX, float radiusY, float rotationAngle, CanvasSweepDirection sweepDirection, CanvasArcSize arcSize)
        {
            _builder.AppendFormat("A {0} {1} {2} {3} {4} {5} {6} ", 
                radiusX, radiusY, rotationAngle, (int)arcSize, (int)sweepDirection, endPoint.X, endPoint.Y);
        }

        public void AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint)
        {
            _builder.AppendFormat("C {0} {1} {2} {3} {4} {5} ", 
                controlPoint1.X, controlPoint1.Y, controlPoint2.X, controlPoint2.Y, endPoint.X, endPoint.Y);
        }

        public void AddLine(Vector2 endPoint)
        {
            _builder.AppendFormat("L {0} {1} ", endPoint.X, endPoint.Y);
        }

        public void AddQuadraticBezier(Vector2 controlPoint, Vector2 endPoint)
        {
            _builder.AppendFormat("Q {0} {1} {2} {3} ", controlPoint.X, controlPoint.Y, endPoint.X, endPoint.Y);
        }

        public void SetFilledRegionDetermination(CanvasFilledRegionDetermination filledRegionDetermination)
        {
            if (_builder.Length == 0)
                return;

            _builder.AppendFormat("F {0} ", (int)filledRegionDetermination);
        }

        public void SetSegmentOptions(CanvasFigureSegmentOptions figureSegmentOptions)
        {
            //throw new NotImplementedException();
        }

        public void EndFigure(CanvasFigureLoop figureLoop)
        {
            _builder.Append("Z ");
        }

        public string GetPathData()
        {
            return _builder.ToString();
        }
    }
}
