using IfcToolbox.Core.Entities;
using IfcToolbox.Core.Extensions;
using Xbim.Ifc;
using Xunit;

namespace IfcToolbox.Tests
{
    public class EntityExtensionTests
    {
        [Theory]
        [InlineData(11, 14)] // Location with refs
        [InlineData(124, 139)] // 3D Points
        public void EntityPropsEqualityShouldPass(int index1, int index2)
        {
            using (var store = IfcStore.Open(LocalFiles.Ifc4_CubeAdvancedBrep))
            {
                var point1 = store.Instances[index1];
                var point2 = store.Instances[index2];

                Assert.True(point1.PropertiesEquals(point2));
            }
        }

        [Theory]
        [InlineData(11, true)] // Location with refs
        [InlineData(124, false)] // 3D Points
        public void EntityHasReferencesShouldPass(int index, bool expected)
        {
            using (var store = IfcStore.Open(LocalFiles.Ifc4_CubeAdvancedBrep))
            {
                var entity = store.Instances[index];
                Assert.True(entity.HasReference() == expected);
            }
        }

        // Both pairs are byte-identical entities in Ifc4_CubeAdvancedBrep, so EntityProxy
        // equality has to agree with PropertiesEquals above.
        // The previous data (378/403, 18/19) could never pass: labels 378 and 403 do not exist
        // in this file (it stops at 184), and 18/19 are IfcSiUnits, which EntityProxy.Equals
        // refuses to treat as equal by design.
        [Theory]
        [InlineData(11, 14)] // IfcAxis2Placement3D with identical refs
        [InlineData(124, 139)] // identical 3D points
        public void EntityProxyShouldEqual(int index1, int index2)
        {
            using (var store = IfcStore.Open(LocalFiles.Ifc4_CubeAdvancedBrep))
            {
                var point1 = new EntityProxy(store.Instances[index1]);
                var point2 = new EntityProxy(store.Instances[index2]);

                Assert.True(point1.Equals(point2));
            }
        }
    }
}