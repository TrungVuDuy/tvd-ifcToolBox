using System;
using System.Collections.Generic;
using Xbim.Common;
using Xbim.Ifc;
using IfcToolbox.Core.Utilities;
using Xbim.IO;

namespace IfcToolbox.Core.Editors
{
    public class InsertCopy
    {
        /// <summary>
        /// Storage backend for the temporary model that receives the copy.
        /// Esent is deliberately not used: under Xbim 6 its InsertCopy path throws
        /// InvalidCastException (List&lt;IPersist&gt; -> IExpressEnumerable) as soon as products
        /// are copied with geometry, which broke every split strategy except DataOnly.
        /// Trade-off: a memory backed model needs more RAM on very large files.
        /// </summary>
        private const XbimStoreType CopyStoreType = XbimStoreType.InMemoryModel;

        /// <summary>
        /// SaveAs contain Transaction, need to out of other Transactions.
        /// </summary>
        public static void SaveAs(IfcStore model, string filePath, bool deleteOld = true, bool keepLabel = false)
        {
            using (var iModel = IfcStore.Create(ToolboxFiles.Credentials(), ((IModel)model).SchemaVersion, CopyStoreType))
            {
                using (var cache = model.BeginInverseCaching())
                using (var txn = iModel.BeginTransaction("InsertCopy with caching"))
                {
                    var map = new XbimInstanceHandleMap(model, iModel);
                    if (deleteOld)
                    {
                        // The Xbim 6 interface layer is schema independent, so no branch per schema.
                        iModel.InsertCopy(model.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>(),
                                true, keepLabel, map);
                    }
                    else
                    {
                        foreach (var instance in model.Instances)
                            iModel.InsertCopy(instance, map, SemanticFilter.GetAll(), true, keepLabel);
                    }
                    txn.Commit();
                }
                UpdateHeader(model, iModel);
                ToolboxFiles.Save(iModel, filePath);
            }
        }

        public static void CopyProducts(IfcStore model, string filePath, IEnumerable<Xbim.Ifc4.Interfaces.IIfcProduct> products, bool keepLabel = false)
        {
            using (var iModel = IfcStore.Create(ToolboxFiles.Credentials(), ((IModel)model).SchemaVersion, CopyStoreType))
            {
                using (model.BeginEntityCaching())
                using (model.BeginInverseCaching())
                using (var txn = iModel.BeginTransaction("InsertCopy with IfcProducts"))
                {
                    var map = new XbimInstanceHandleMap(model, iModel);
                    iModel.InsertCopy(products, true, keepLabel, map);
                    txn.Commit();
                }
                UpdateHeader(model, iModel);
                ToolboxFiles.Save(iModel, filePath);
            }
        }

        public static void CopyOnlyProperties(IfcStore model, string filePath, bool keepLabel = false)
        {
            using (var iModel = IfcStore.Create(ToolboxFiles.Credentials(), ((IModel)model).SchemaVersion, CopyStoreType))
            {
                using (model.BeginEntityCaching())
                using (model.BeginInverseCaching())
                using (var txn = iModel.BeginTransaction("InsertCopy without geometry"))
                {
                    var map = new XbimInstanceHandleMap(model, iModel);
                    iModel.InsertCopy(model.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>(), false, keepLabel, map);
                    txn.Commit();
                }
                UpdateHeader(model, iModel);
                ToolboxFiles.Save(iModel, filePath);
            }
        }

        private static void UpdateHeader(IfcStore source, IfcStore target)
        {
            target.Header.FileDescription = source.Header.FileDescription;
            target.Header.FileName = source.Header.FileName;
            target.Header.FileSchema = source.Header.FileSchema;
            target.Header.FileName.OriginatingSystem = source.Header.FileName.OriginatingSystem;
        }
    }
}

