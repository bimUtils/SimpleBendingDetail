using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleBendingDetail
{
    internal class CurveBoundingBoxXYZ
    {
        public XYZ Min { get; set; }
        public XYZ Max { get; set; }

        public CurveBoundingBoxXYZ()
        {
            Min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue);
            Max = new XYZ(double.MinValue, double.MinValue, double.MinValue);
        }

        public void AddPoint(XYZ point)
        {
            double x = Min.X;
            double y = Min.Y;
            double z = Min.Z;

            if (point.X < x) x = point.X;
            if (point.Y < y) y = point.Y;
            if (point.Z < z) z = point.Z;
            Min = new XYZ(x, y, z);

            x = Max.X;
            y = Max.Y;
            z = Max.Z;

            if (point.X > x) x = point.X;
            if (point.Y > y) y = point.Y;
            if (point.Z > z) z = point.Z;
            Max = new XYZ(x, y, z);
        }

        public void AddCurve(Curve curve)
        {

            if (curve.IsCyclic)
            {
                //set boundind box around arc extreme points
                XYZ center;
                XYZ x;
                XYZ y;
                double r;
                Arc arc = curve as Arc;

                x = arc.XDirection;
                y = arc.YDirection;
                r = arc.Radius;
                center = arc.Center;

                double startParameter = curve.GetEndParameter(0);
                double endParameter = curve.GetEndParameter(1);
                double thetaX, thetaY, thetaZ;

                if (x.X != 0)
                    thetaX = Math.Atan(y.X / x.X);
                else
                    thetaX = Math.PI / 2;
                while (thetaX > startParameter)
                {
                    thetaX -= Math.PI;
                }
                while (thetaX < endParameter)
                {
                    XYZ point = center + r * Math.Cos(thetaX) * x + r * Math.Sin(thetaX) * y;
                    if (curve.IsInside(thetaX))
                        AddPoint(point);
                    thetaX += Math.PI;
                }

                if (x.Y != 0)
                    thetaY = Math.Atan(y.Y / x.Y);
                else
                    thetaY = Math.PI / 2;
                while (thetaY > startParameter)
                {
                    thetaY -= Math.PI;
                }
                while (thetaY < endParameter)
                {
                    XYZ point = center + r * Math.Cos(thetaY) * x + r * Math.Sin(thetaY) * y;
                    if (curve.IsInside(thetaY))
                        AddPoint(point);
                    thetaY += Math.PI;
                }

                if (x.Z != 0)
                    thetaZ = Math.Atan(y.Z / x.Z);
                else
                    thetaZ = Math.PI / 2;
                while (thetaZ > startParameter)
                {
                    thetaZ -= Math.PI;
                }
                while (thetaZ < endParameter)
                {
                    XYZ point = center + r * Math.Cos(thetaZ) * x + r * Math.Sin(thetaZ) * y;
                    if (curve.IsInside(thetaZ))
                        AddPoint(point);
                    thetaZ += Math.PI;
                }

            }

            //add a start and end point, whatever it is a line or an arc
            AddPoint(curve.GetEndPoint(0));
            AddPoint(curve.GetEndPoint(1));

        }

    }
}
