module ShadedQuad

open System.Threading.Tasks
open Ground

let CreateQuadScene () =
    InitScene()

    let vertices = [|
        0.0f; 0.0f; 0.0f;
        1.0f; 0.0f; 0.0f;
        1.0f; 1.0f; 0.0f;
        0.0f; 1.0f; 0.0f;
    |]
    let indices = [|
        0; 1; 2;
        0; 2; 3
    |]
    let quadId = AddTriangleMesh(vertices, 4, indices, 6)

    FinalizeScene()
    quadId

let CreateMaterial quadId =
    InitShadingSystem 1 // monochromatic rendering for now

    // Colors are always textures, so we create a 1x1 image
    let color = [| 0.5f |]
    let baseTex = CreateImage(1, 1, 1)
    AddSplat (baseTex, 0.0f, 0.0f, color)

    // Now we create a material with constant diffuse color and no emission
    let mutable matParams =
        UberShaderParams(
            baseColorTexture = baseTex,
            emissionTexture = -1)
    let matId = AddUberMaterial &matParams

    // Assign the material to the quad
    AssignMaterial (quadId, matId)

[<AbstractClass>]
type Camera (imgW : int, imgH : int) =
    member this.ImageWidth = imgW
    member this.ImageHeight = imgH
    abstract member GenerateRay : float32 * float32 -> float32[] * float32[]

type OrthographicCamera (topLeft : float32[], diag : float32[], imgW : int, imgH : int) =
    inherit Camera (imgW, imgH)
    override this.GenerateRay (x:float32, y:float32) =
        let org = [|
            topLeft.[0] + float32(x) / float32(imgW) * diag.[0];
            topLeft.[1] + float32(y) / float32(imgH) * diag.[1];
            5.0f
        |]
        let dir = [| 0.0f; 0.0f; -1.0f |]
        (org, dir)

type HitCallback = (Hit * float32 * float32) -> unit

let TraceRays (camera:Camera, report:HitCallback) =
    Parallel.For(0, camera.ImageHeight, fun y ->
        for x in 0 .. camera.ImageWidth - 1 do
            let ray = camera.GenerateRay (float32(x), float32(y))
            let hit = TraceSingle ray
            report (hit, float32(x), float32(y))
    ) |> ignore

let SetupCamera (imageWidth:int, imageHeight:int) =
    let topLeft  = [| -1.0f;  -1.0f; 5.0f |]
    let diag = [|  3.0f; 3.0f; 0.0f |]
    OrthographicCamera(topLeft, diag, imageWidth, imageHeight)

let Render () =
    let quadId = CreateQuadScene ()
    CreateMaterial quadId

    let imageWidth = 512
    let imageHeight = 512
    let frameBuf = CreateImage(imageWidth, imageHeight, 1)

    let camera = SetupCamera (imageWidth, imageHeight)

    TraceRays(camera, fun (hit, x, y) ->
        let value = [| float32(hit.meshId) |]
        AddSplat(frameBuf, x, y, value))

    WriteImage(frameBuf, "renderFS.exr")