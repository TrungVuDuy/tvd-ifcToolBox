using Serilog;
using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Editors
{
    public class PointOptimization
    {
        #region OptimizePoints
        /// <summary>
        /// Merges coincident IfcCartesianPoints and, when <paramref name="withPrecision"/> is set,
        /// rewrites them at the requested number of decimals.
        /// Works for every schema: in Xbim 6 IFC2x3 entities implement the Xbim.Ifc4 interfaces too,
        /// which is why the former per-schema overloads (and PointProxy) are gone.
        /// </summary>
        public static Dictionary<IPersistEntity, IPersistEntity> OptimizePoints(IModel model, IEnumerable<IIfcCartesianPoint> points, bool withPrecision = true, int precision = 4)
        {
            var groupedEntities = new Dictionary<IIfcCartesianPoint, IList<IIfcCartesianPoint>>();

            var groups = points.GroupBy(p => new { p.X, p.Y, p.Z, p.Dim });
            foreach (var group in groups)
            {
                var groupList = group.ToList();
                var groupFirst = group.First();
                groupList.Remove(groupFirst);
                groupedEntities.Add(groupFirst, groupList);
            }

            var createdPoints = CreateNewPoints(model, groupedEntities, withPrecision, precision);

            var duplicatedGroups = groupedEntities
              .Where(group => group.Value.Count() > 0)
              .ToDictionary(group => group.Key, group => group.Value);

            var entityMap = new Dictionary<IPersistEntity, IPersistEntity>();
            foreach (var duplicatedGroup in duplicatedGroups)
                foreach (var duplicatedEntity in duplicatedGroup.Value)
                    if (duplicatedEntity.EntityLabel != duplicatedGroup.Key.EntityLabel)
                        entityMap.Add(duplicatedEntity, duplicatedGroup.Key);

            var updatedEntities = points.ToList();
            updatedEntities.AddRange(createdPoints);
            var replacer = new EntityReplacer();
            replacer.ReplaceEntities(model, updatedEntities, entityMap);
            return entityMap;
        }

        private static List<IIfcCartesianPoint> CreateNewPoints(IModel model, Dictionary<IIfcCartesianPoint, IList<IIfcCartesianPoint>> groupedEntities, bool withPrecision, int precision)
        {
            var createdPointsDic = new Dictionary<IIfcCartesianPoint, IIfcCartesianPoint>();
            if (withPrecision)
            {
                foreach (var point in groupedEntities.Keys)
                    if (PointPrecision.NeedOptimize(point, precision))
                    {
                        var newPoint = PointFactory.CreatePoint(model, point, withPrecision, precision);
                        if (newPoint != null)
                            createdPointsDic.Add(point, newPoint);
                    }
                foreach (var point in createdPointsDic.Keys)
                    if (groupedEntities.ContainsKey(point))
                    {
                        var updatedList = groupedEntities[point];
                        updatedList.Add(point);
                        groupedEntities.Add(createdPointsDic[point], updatedList);
                        groupedEntities.Remove(point);
                    }
            }
            return createdPointsDic.Values.ToList();
        }
        #endregion

        #region OptimizePointLists
        public static Dictionary<IPersistEntity, IPersistEntity> OptimizePointLists(IModel model, bool withPrecision = true, int precision = 4)
        {
            var entityMap = new Dictionary<IPersistEntity, IPersistEntity>();
            if (!withPrecision)
                return entityMap;

            List<Xbim.Ifc4.Interfaces.IIfcCartesianPointList> pointLists = model.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcCartesianPointList>().ToList();

            var createdPointLists = new List<Xbim.Ifc4.Interfaces.IIfcCartesianPointList>();
            foreach (var pointList in pointLists)
            {
                List<List<double>> valueTable = GetPointListValue(pointList, precision);
                if (pointList is Xbim.Ifc4.Interfaces.IIfcCartesianPointList3D)
                {
                    var createdPointList = CreatePointList3D(model, valueTable);
                    createdPointLists.Add(createdPointList);
                    entityMap.Add(pointList, createdPointList);
                }
                else if (pointList is Xbim.Ifc4.Interfaces.IIfcCartesianPointList2D)
                {
                    var createdPointList = CreatePointList2D(model, valueTable);
                    createdPointLists.Add(createdPointList);
                    entityMap.Add(pointList, createdPointList);
                }
            }

            pointLists.AddRange(createdPointLists);
            var replacer = new EntityReplacer();
            replacer.ReplaceEntities(model, pointLists, entityMap);
            return entityMap;
        }

        private static Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList3D CreatePointList3D(IModel model, List<List<double>> table)
        {
            Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList3D pointList = model.Instances.New<Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList3D>(cpl =>
            {
                for (int i = 0; i < table.Count; i++)
                {
                    var values = table[i];
                    Xbim.Ifc4.MeasureResource.IfcLengthMeasure[] measures = new Xbim.Ifc4.MeasureResource.IfcLengthMeasure[values.Count];
                    for (int j = 0; j < values.Count; j++)
                        measures[j] = new Xbim.Ifc4.MeasureResource.IfcLengthMeasure(values[j]);
                    cpl.CoordList.GetAt(i).AddRange(measures);
                }
            });
            return pointList;
        }
        private static Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList2D CreatePointList2D(IModel model, List<List<double>> table)
        {
            Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList2D pointList = model.Instances.New<Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList2D>(cpl =>
            {
                for (int i = 0; i < table.Count; i++)
                {
                    var values = table[i];
                    Xbim.Ifc4.MeasureResource.IfcLengthMeasure[] measures = new Xbim.Ifc4.MeasureResource.IfcLengthMeasure[values.Count];
                    for (int j = 0; j < values.Count; j++)
                        measures[j] = new Xbim.Ifc4.MeasureResource.IfcLengthMeasure(values[j]);
                    cpl.CoordList.GetAt(i).AddRange(measures);
                }
            });
            return pointList;
        }

        private static List<List<double>> GetPointListValue(Xbim.Ifc4.Interfaces.IIfcCartesianPointList entite, int precision)
        {
            List<List<double>> table = new List<List<double>>();
            if (entite is Xbim.Ifc4.Interfaces.IIfcCartesianPointList3D pointList3d)
            {
                foreach (var coordList in pointList3d.CoordList)
                {
                    var values = coordList.Select(x => double.Parse(x.Value.ToString())).ToList();
                    table.Add(PointPrecision.Optimize(precision, values));
                }
            }
            else if (entite is Xbim.Ifc4.Interfaces.IIfcCartesianPointList2D pointList2d)
            {
                foreach (var coordList in pointList2d.CoordList)
                {
                    var values = coordList.Select(x => double.Parse(x.Value.ToString())).ToList();
                    table.Add(PointPrecision.Optimize(precision, values));
                }
            }
            return table;
        }

        public static void LogMergeMap(Dictionary<IPersistEntity, IPersistEntity> mergeMap)
        {
            Log.Information($"MergeMap Count: {mergeMap.Count()}");
            foreach (var item in mergeMap)
                Log.Information($"Mapping: {item.Key} --> {item.Value}");
        }
        #endregion
    }
}
