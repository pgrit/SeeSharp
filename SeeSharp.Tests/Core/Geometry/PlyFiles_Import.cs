using SeeSharp.Geometry;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace SeeSharp.Tests.Core.Geometry {
    public class PlyFiles_Import {
        [Fact]
        public void SimplePly_ShouldBeReadAscii() {
            CreateTestAsciiPly();
            PlyFile file = new();
            var errors = file.ParseFile("test.ply");
            Assert.Empty(errors);

            var mesh = file.ToMesh();

            // The mesh should NOT have a material assigned
            Assert.Null(mesh.Material);
        }

        [Fact]
        public void SimplePly_ShouldBeReadBinary() {
            CreateTestBinaryPly();
            PlyFile file = new();
            var errors = file.ParseFile("test.ply");
            Assert.Empty(errors);

            var mesh = file.ToMesh();

            // The mesh should NOT have a material assigned
            Assert.Null(mesh.Material);
        }

        [Fact]
        public void SimplePly_TriangulationAscii() {
            CreateTestAsciiPly();
            PlyFile file = new();
            var errors = file.ParseFile("test.ply");
            Assert.Empty(errors);

            var mesh = file.ToMesh();

            // There should be exactly 12 triangles
            Assert.Equal(12, mesh.NumFaces);
            // There should be exactly 8 vertices
            Assert.Equal(8, mesh.NumVertices);
        }

        [Fact]
        public void SimplePly_TriangulationBinary() {
            CreateTestBinaryPly();
            PlyFile file = new();
            var errors = file.ParseFile("test.ply");
            Assert.Empty(errors);

            var mesh = file.ToMesh();

            // There should be exactly 12 triangles
            Assert.Equal(12, mesh.NumFaces);
            // There should be exactly 8 vertices
            Assert.Equal(8, mesh.NumVertices);
        }

        static void CreateTestAsciiPly() {
            string plyCode = @"ply
format ascii 1.0                       
comment made by me, myself and I 
comment this file is a cube I suppose
element vertex 8                        
property float x                        
property float y                       
property float z                      
element face 6                          
property list uchar int vertex_indices 
end_header                            
0 0 0                            
0 0 1
0 1 1
0 1 0
1 0 0
1 0 1
1 1 1
1 1 0
4 0 1 2 3
4 7 6 5 4
4 0 4 5 1
4 1 5 6 2
4 2 6 7 3
4 3 7 4 0";

            System.IO.File.WriteAllText(@"test.ply", plyCode);
        }


        static void CreateTestBinaryPly() {
            string format = BitConverter.IsLittleEndian ? "binary_little_endian" : "binary_big_endian";
            string plyHeader = $@"ply
format {format} 1.0                       
comment made by me, myself and I 
comment this file is a cube I suppose
element vertex 8                        
property float x                        
property float y                       
property float z                      
element face 6                          
property list uchar int vertex_indices 
end_header
";

            List<byte> data = new();
            data.AddRange(Encoding.ASCII.GetBytes(plyHeader));

            // Vertices
            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)0));

            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)1));

            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)1));

            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)0));

            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)0));

            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)0));
            data.AddRange(BitConverter.GetBytes((float)1));

            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)1));

            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)1));
            data.AddRange(BitConverter.GetBytes((float)0));

            // Indices
            data.Add(4);
            data.AddRange(BitConverter.GetBytes((int)0));
            data.AddRange(BitConverter.GetBytes((int)1));
            data.AddRange(BitConverter.GetBytes((int)2));
            data.AddRange(BitConverter.GetBytes((int)3));

            data.Add(4);
            data.AddRange(BitConverter.GetBytes((int)7));
            data.AddRange(BitConverter.GetBytes((int)6));
            data.AddRange(BitConverter.GetBytes((int)5));
            data.AddRange(BitConverter.GetBytes((int)4));

            data.Add(4);
            data.AddRange(BitConverter.GetBytes((int)0));
            data.AddRange(BitConverter.GetBytes((int)4));
            data.AddRange(BitConverter.GetBytes((int)5));
            data.AddRange(BitConverter.GetBytes((int)1));

            data.Add(4);
            data.AddRange(BitConverter.GetBytes((int)1));
            data.AddRange(BitConverter.GetBytes((int)5));
            data.AddRange(BitConverter.GetBytes((int)6));
            data.AddRange(BitConverter.GetBytes((int)2));

            data.Add(4);
            data.AddRange(BitConverter.GetBytes((int)2));
            data.AddRange(BitConverter.GetBytes((int)6));
            data.AddRange(BitConverter.GetBytes((int)7));
            data.AddRange(BitConverter.GetBytes((int)3));

            data.Add(4);
            data.AddRange(BitConverter.GetBytes((int)3));
            data.AddRange(BitConverter.GetBytes((int)7));
            data.AddRange(BitConverter.GetBytes((int)4));
            data.AddRange(BitConverter.GetBytes((int)0));

            System.IO.File.WriteAllBytes(@"test.ply", data.ToArray());
        }
    }
}
