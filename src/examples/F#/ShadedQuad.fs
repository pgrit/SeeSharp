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

type Vector = float32 * float32 * float32

type OrthographicCamera (topLeft:Vector, diag:Vector, imgW:int, imgH:int) =
    inherit Camera (imgW, imgH)
    override this.GenerateRay (x:float32, y:float32) =
        let (top, left, depth) = topLeft
        let (diagx, diagy, _) = diag
        let org = [|
            top + float32(x) / float32(imgW) * diagx;
            left + float32(y) / float32(imgH) * diagy;
            depth
        |]
        let dir = [| 0.0f; 0.0f; -1.0f |]
        (org, dir)

type Spectrum = float32
type Ray = (float32[] * float32[])

let rec OutgoingRadiance (hit:Hit, ray:Ray) =
    if hit.meshId < 0 then
        1.0f
    else
        // Sample a direction from the BSDF
        // Trace a ray and recurse
        let nextRay = ray
        let bsdfValue = [| 0.0f |]
        SampleBsdf(hit, ray, nextRay, bsdfValue)
        
        let nextHit = TraceSingle ray
        OutgoingRadiance (nextHit, nextRay)
        0.0f

let TracePaths (camera:Camera, frameBuf:int) =
    Parallel.For(0, camera.ImageHeight, fun y ->
        for x in 0 .. camera.ImageWidth - 1 do            
            let ray = camera.GenerateRay (float32(x), float32(y))
            let hit = TraceSingle ray
            let value = [| OutgoingRadiance (hit, ray) |]
            AddSplat(frameBuf, float32(x), float32(y), value)
    ) |> ignore

let SetupCamera (imageWidth:int, imageHeight:int) =
    let topLeft  = (-1.0f, -1.0f, 5.0f)
    let diag = (3.0f, 3.0f, 0.0f)
    OrthographicCamera(topLeft, diag, imageWidth, imageHeight)

let Render () =
    let quadId = CreateQuadScene ()
    CreateMaterial quadId

    let imageWidth = 512
    let imageHeight = 512
    let frameBuf = CreateImage(imageWidth, imageHeight, 1)

    let camera = SetupCamera (imageWidth, imageHeight)

    TracePaths (camera, frameBuf)

    WriteImage(frameBuf, "renderFS.exr")