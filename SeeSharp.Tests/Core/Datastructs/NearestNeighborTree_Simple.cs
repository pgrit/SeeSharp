using System.Numerics;
using SeeSharp.Datastructs;
using Xunit;

namespace SeeSharp.Tests.Datastructs {
    public class NearestNeighborTree_Simple {
        [Fact]
        public void SinglePoint_ShouldBeFound() {
            //Given
            var tree = new NearestNeighborTree();
            var p = new Vector3(14, 2.3f, 1.9f);
            tree.AddPoint(p, 13);
            tree.Build();

            //When
            var result = tree.QueryNearest(Vector3.Zero, 1, float.MaxValue);

            //Then
            Assert.Single(result);
            Assert.Equal(13, result[0]);
        }

        [Fact]
        public void SinglePoint_OutsideRadius() {
            //Given
            var tree = new NearestNeighborTree();
            var p = new Vector3(14, 2.3f, 1.9f);
            tree.AddPoint(p, 13);
            tree.Build();

            //When
            var result = tree.QueryNearest(Vector3.Zero, 1, 0.1f);

            //Then
            Assert.Empty(result);
        }

        [Fact]
        public void TwoPoints_OnlyClosestShouldBeFound() {
            //Given
            var tree = new NearestNeighborTree();
            var p = new Vector3(14, 2.3f, 1.9f);
            var pfar = new Vector3(184, 2901, 231);
            tree.AddPoint(p, 13);
            tree.AddPoint(pfar, 1);
            tree.Build();

            //When
            var result = tree.QueryNearest(Vector3.Zero, 1, float.MaxValue);

            //Then
            Assert.Single(result);
            Assert.Equal(13, result[0]);
        }

        [Fact]
        public void TwoPoints_BothShouldBeFound() {
            //Given
            var tree = new NearestNeighborTree();
            var p = new Vector3(14, 2.3f, 1.9f);
            var pfar = new Vector3(184, 2901, 231);
            tree.AddPoint(p, 13);
            tree.AddPoint(pfar, 1);
            tree.Build();

            //When
            var result = tree.QueryNearest(Vector3.Zero, 2, float.MaxValue);

            //Then
            Assert.Equal(2, result.Length);
            Assert.Equal(13, result[0]);
            Assert.Equal(1, result[1]);
        }

        [Fact]
        public void TwoPoints_OnlyInRadiusShouldBeFound() {
            //Given
            var tree = new NearestNeighborTree();
            var p = new Vector3(14, 2.3f, 1.9f);
            var pfar = new Vector3(184, 2901, 231);
            tree.AddPoint(p, 13);
            tree.AddPoint(pfar, 1);
            tree.Build();

            //When
            var result = tree.QueryNearest(Vector3.Zero, 2, 20.0f);

            //Then
            Assert.Single(result);
            Assert.Equal(13, result[0]);
        }

        [Fact]
        public void Clear_ShouldBeEmpty() {
            //Given
            var tree = new NearestNeighborTree();
            var p = new Vector3(14, 2.3f, 1.9f);
            var pfar = new Vector3(184, 2901, 231);
            tree.AddPoint(p, 13);
            tree.AddPoint(pfar, 1);
            tree.Build();

            //When
            tree.Clear();
            var result = tree.QueryNearest(Vector3.Zero, 2, float.MaxValue);

            //Then
            Assert.Empty(result);
        }
    }
}