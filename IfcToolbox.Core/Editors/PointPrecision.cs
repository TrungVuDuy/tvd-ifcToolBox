using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IfcToolbox.Core.Editors
{
    public static class PointPrecision
    {
        public static List<double> Optimize(int precision, List<double> coordinates)
        {
            List<double> newCoordiantes = new List<double>();
            foreach (var item in coordinates)
                newCoordiantes.Add(Math.Round(item, precision));
            return newCoordiantes;
        }

        // Xbim 6 exposes a single interface layer (Xbim.Ifc4.Interfaces) for every schema,
        // so one overload now serves IFC2x3, IFC4 and IFC4x3 alike.
        public static bool NeedOptimize(Xbim.Ifc4.Interfaces.IIfcCartesianPoint point, int precision)
        {
            var values = point.Coordinates.Select(o => (double)o.Value).ToList();
            return NeedOptimize(values, precision);
        }

        private static bool NeedOptimize(List<double> coordinates, int precision)
        {
            if (!coordinates.Any()) return false;
            if (coordinates.Count() < 3)
            {
                if (GetDoublePlaces(coordinates[0]) > precision || GetDoublePlaces(coordinates[1]) > precision)
                    return true;
            }
            else
            {
                if (GetDoublePlaces(coordinates[0]) > precision || GetDoublePlaces(coordinates[1]) > precision || GetDoublePlaces(coordinates[2]) > precision)
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Number of decimal places needed to write the value exactly.
        /// <para>
        /// Formats with the invariant culture: the previous version used the machine's culture
        /// and looked for '.', so on a system using a decimal comma (vi-VN, de-DE, fr-FR...) it
        /// always found zero places and the optimizer silently never rounded anything.
        /// Scientific notation (1E-05) is handled as well.
        /// </para>
        /// </summary>
        public static int GetDoublePlaces(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            var text = value.ToString("R", CultureInfo.InvariantCulture);
            int exponent = 0;
            int e = text.IndexOfAny(new[] { 'E', 'e' });
            if (e >= 0)
            {
                exponent = int.Parse(text.Substring(e + 1), CultureInfo.InvariantCulture);
                text = text.Substring(0, e);
            }

            int dot = text.IndexOf('.');
            int decimals = dot < 0 ? 0 : text.Length - dot - 1;
            return Math.Max(0, decimals - exponent);
        }
    }
}
