using SeeSharp.IO;
using Xunit;

namespace SeeSharp.Tests.Core.Geometry;

public class ObjFiles_Import {

    [Fact]
    public void SimpleObj_ShouldBeRead() {
        CreateTestObj();
        var mesh = ObjMesh.FromFile("test.obj");
        var dummyScene = new Scene();
        ObjConverter.AddToScene(mesh, dummyScene, null);

        Assert.Empty(mesh.Errors);

        // There should be some meshes
        Assert.NotEmpty(dummyScene.Meshes);

        // Every mesh should have a material assigned
        foreach (var m in dummyScene.Meshes) {
            Assert.NotNull(m.Material);
        }

        // There should be an emitter
        Assert.NotEmpty(dummyScene.Emitters);
    }

    [Fact]
    public void SimpleObj_Triangulation() {
        CreateTestObj();
        var mesh = ObjMesh.FromFile("test.obj");
        var dummyScene = new Scene();
        ObjConverter.AddToScene(mesh, dummyScene, null);

        // There should be exactly 7 triangles
        int numTriangles = 0;
        foreach (var m in dummyScene.Meshes) {
            numTriangles += m.NumFaces;
        }

        Assert.Equal(7, numTriangles);
    }

    [Fact]
    public void SimpleObj_OneMeshPerMaterialGroup() {
        CreateTestObj();
        var mesh = ObjMesh.FromFile("test.obj");
        var dummyScene = new Scene();
        ObjConverter.AddToScene(mesh, dummyScene, null);

        // There should be four meshes in total (one per group, except if there are multiple
        // materials in the same group)
        Assert.Equal(4, dummyScene.Meshes.Count);
    }

    static void CreateTestObj() {
        string objCode = "mtllib test.mtl\n";

        // a lone triangle
        objCode += "v -1 0 -1" + "\n";
        objCode += "v  1 0 -1" + "\n";
        objCode += "v  0 1 -1" + "\n";
        objCode += "g loner" + "\n";
        objCode += "usemtl surface" + "\n";
        objCode += "f 1 2 3" + "\n";

        // above two quads with the same material
        objCode += "v -1 -1 -2" + "\n";
        objCode += "v  0 -1 -2" + "\n";
        objCode += "v  0  1 -2" + "\n";
        objCode += "v -1  1 -2" + "\n";
        objCode += "v  0 -1 -2" + "\n";
        objCode += "v  1 -1 -2" + "\n";
        objCode += "v  1  1 -2" + "\n";
        objCode += "v  0  1 -2" + "\n";
        objCode += "g group" + "\n";
        objCode += "usemtl surface" + "\n";
        objCode += "f 4 5 6 7" + "\n"; // should be split into two triangles
        objCode += "usemtl otherSurface" + "\n";
        objCode += "f 8 9 10" + "\n";
        objCode += "f 8 10 11" + "\n";

        // illuminated by a quad light source
        objCode += "v -0.1 -0.1 2" + "\n" + "vn 0 0 -1" + "\n";
        objCode += "v  0.1 -0.1 2" + "\n" + "vn 0 0 -1" + "\n";
        objCode += "v  0.1  0.1 2" + "\n" + "vn 0 0 -1" + "\n";
        objCode += "v -0.1  0.1 2" + "\n" + "vn 0 0 -1" + "\n";
        objCode += "g light \n";
        objCode += "usemtl light \n";
        objCode += "f 12//1 13//2 14//3 15//4 \n";

        string mtlCode = @"
                newmtl surface
                Kd 0.9 0.7 0
                newmtl otherSurface
                Kd 0.1 0.1 0.9
                newmtl light
                Ke 10 10 10
                Kd 0 0 0
                ";

        System.IO.File.WriteAllText(@"test.obj", objCode);
        System.IO.File.WriteAllText(@"test.mtl", mtlCode);
    }
}