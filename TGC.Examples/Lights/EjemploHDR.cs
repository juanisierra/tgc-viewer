using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Collections.Generic;
using System.Drawing;
using TGC.Core;
using TGC.Core.Camara;
using TGC.Core.Direct3D;
using TGC.Core.Example;
using TGC.Core.Geometry;
using TGC.Core.SceneLoader;
using TGC.Core.Shaders;
using TGC.Core.UserControls;
using TGC.Core.UserControls.Modifier;
using TGC.Core.Utils;

namespace TGC.Examples.Lights
{
    /// <summary>
    ///     Ejemplo EjemploHDR:
    ///     Unidades Involucradas:
    ///     # Unidad 4 - Texturas e Iluminaci�n - Iluminaci�n din�mica
    ///     # Unidad 8 - Adaptadores de Video - Shaders
    ///     Ejemplo avanzado. Ver primero ejemplo "Lights/EjemploPointLight" y "PostProcess/EfectoGaussianBlur"
    ///     Autor: Mat�as Leone, Leandro Barbagallo
    /// </summary>
    public class EfectoGaussianBlur : TgcExample
    {
        private const int NUM_LUMINANCE_TEXTURES = 4;
        private Texture bloomRT;
        private Texture bloomTempRT;
        private Texture brightPassRT;
        private Effect effect;
        private TgcBox lightMesh;
        private Texture[] luminanceRTs;
        private List<TgcMesh> meshes;
        private Surface pOldRT;
        private Texture scaledSceneRT;
        private Texture sceneRT;

        private TgcScreenQuad screenQuad;

        public EfectoGaussianBlur(string mediaDir, string shadersDir, TgcUserVars userVars, TgcModifiers modifiers,
            TgcAxisLines axisLines, TgcCamera camara)
            : base(mediaDir, shadersDir, userVars, modifiers, axisLines, camara)
        {
            Category = "Lights";
            Name = "HDR";
            Description = "HDR";
        }

        public override void Init()
        {
            //Creamos un FullScreen Quad
            screenQuad = new TgcScreenQuad();

            //Crear RT con formato de floating point en vez del tipico formato de enteros
            var backBufferWidth = D3DDevice.Instance.Device.PresentationParameters.BackBufferWidth;
            var backBufferHeight = D3DDevice.Instance.Device.PresentationParameters.BackBufferHeight;
            sceneRT = new Texture(D3DDevice.Instance.Device, backBufferWidth, backBufferHeight, 1, Usage.RenderTarget,
                Format.A16B16G16R16F, Pool.Default);

            //Crear RT para escalar sceneRT a un tama�o menor, de 1/4 x 1/4 (y divisible por 8 para facilitar los calculos de sampleo)
            var cropWidth = (backBufferWidth - backBufferWidth % 8) / 4;
            var cropHeight = (backBufferHeight - backBufferHeight % 8) / 4;
            scaledSceneRT = new Texture(D3DDevice.Instance.Device, cropWidth, cropHeight, 1, Usage.RenderTarget,
                Format.A16B16G16R16F,
                Pool.Default);

            //Crear RT para el bright-pass filter, de igual tama�o que scaledSceneRT (formato comun)
            brightPassRT = new Texture(D3DDevice.Instance.Device, cropWidth, cropHeight, 1, Usage.RenderTarget,
                Format.A8R8G8B8,
                Pool.Default);

            //Crear RT para el efecto bloom (formato comun). Son dos por la doble pasada del filtro gaussiano
            bloomRT = new Texture(D3DDevice.Instance.Device, cropWidth, cropHeight, 1, Usage.RenderTarget,
                Format.A8R8G8B8, Pool.Default);
            bloomTempRT = new Texture(D3DDevice.Instance.Device, cropWidth, cropHeight, 1, Usage.RenderTarget,
                Format.A8R8G8B8,
                Pool.Default);

            //Crear un RT por cada paso de downsampling necesario para obtener el average luminance (un solo canal de 16 bits)
            luminanceRTs = new Texture[NUM_LUMINANCE_TEXTURES];
            for (var i = 0; i < luminanceRTs.Length; i++)
            {
                var iSampleLen = 1 << (2 * i);
                luminanceRTs[i] = new Texture(D3DDevice.Instance.Device, iSampleLen, iSampleLen, 1, Usage.RenderTarget,
                    Format.R16F,
                    Pool.Default);
            }

            //Cargar shader con efectos de Post-Procesado
            effect = TgcShaders.loadEffect(ShadersDir + "HDR.fx");

            //Cargamos un escenario
            var loader = new TgcSceneLoader();
            var scene =
                loader.loadSceneFromFile(MediaDir +
                                         "MeshCreator\\Scenes\\Deposito\\Deposito-TgcScene.xml");
            meshes = scene.Meshes;

            //Aplicar a cada mesh el shader de luz custom
            foreach (var mesh in scene.Meshes)
            {
                mesh.Effect = effect;
                mesh.Technique = "LightPass";
            }

            //Mesh para la luz
            lightMesh = TgcBox.fromSize(new Vector3(10, 10, 10), Color.Red);
            lightMesh.Effect = effect;
            lightMesh.Technique = "DrawLightSource";

            //Camara en 1ra persona
            Camara = new TgcFpsCamera(new Vector3(-20, 80, 450), 400f, 300f);

            //Modifiers de la luz
            Modifiers.addBoolean("toneMapping", "toneMapping", true);
            Modifiers.addFloat("lightIntensity", 0, 100, 5);
            Modifiers.addFloat("middleGray", 0.1f, 1, 0.72f);
            Modifiers.addVertex3f("lightPos", new Vector3(-400, -200, -400), new Vector3(400, 300, 500),
                new Vector3(60, 35, 250));
        }

        public override void Update()
        {
            PreUpdate();
        }

        public override void Render()
        {
            //Guardar RT original
            pOldRT = D3DDevice.Instance.Device.GetRenderTarget(0);
            ClearTextures();
            //Dibujamos la escena al RT en floating point
            drawSceneToRenderTarget(D3DDevice.Instance.Device);

            //Generar version reducida de la imagen original de la escena
            scaleScene(D3DDevice.Instance.Device);

            //Buscar luminance promedio de la escena
            findAverageLuminance(D3DDevice.Instance.Device);

            //Hacer bright-pass para quedarse con los pixels mas luminosos
            brightPass(D3DDevice.Instance.Device);

            //Hacer blur de bright-pass para generar efecto de bloom
            bloomPass(D3DDevice.Instance.Device);

            //Final render
            finalRender(D3DDevice.Instance.Device);

            D3DDevice.Instance.Device.BeginScene();
            RenderAxis();
            RenderFPS();
            D3DDevice.Instance.Device.EndScene();
            D3DDevice.Instance.Device.Present();
        }

        /// <summary>
        ///     Dibujamos toda la escena pero en vez de a la pantalla, la dibujamos al Render Target con floating point.
        ///     De esa forma todos los calculos de iluminacion que superen 1.0 no son clampeados
        /// </summary>
        private void drawSceneToRenderTarget(Device d3dDevice)
        {
            var pSurf = sceneRT.GetSurfaceLevel(0);
            d3dDevice.SetRenderTarget(0, pSurf);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            //Arrancamos el renderizado
            d3dDevice.BeginScene();

            //Actualzar posici�n de la luz
            var lightPos = (Vector3)Modifiers["lightPos"];
            lightMesh.Position = lightPos;

            //Dibujar mesh de fuente de luz
            lightMesh.Effect.Technique = "DrawLightSource";
            lightMesh.render();

            //Renderizar meshes
            foreach (var mesh in meshes)
            {
                mesh.Effect.Technique = "LightPass";

                //Cargar variables shader de la luz
                mesh.Effect.SetValue("lightPosition", TgcParserUtils.vector3ToFloat4Array(lightPos));
                mesh.Effect.SetValue("eyePosition", TgcParserUtils.vector3ToFloat4Array(Camara.Position));
                mesh.Effect.SetValue("lightIntensity", (float)Modifiers["lightIntensity"]);

                //Cargar variables de shader de Material
                mesh.Effect.SetValue("materialAmbientColor", ColorValue.FromColor(Color.DarkGray));
                mesh.Effect.SetValue("materialSpecularExp", 9f);

                //Renderizar modelo
                mesh.render();
            }

            //Renderizar mesh de luz
            lightMesh.render();

            d3dDevice.EndScene();

            pSurf.Dispose();
        }

        /// <summary>
        ///     Generar imagen reducida del RT de HDR original de la escena.
        ///     Lo reducimos a una textura de 1/4 x 1/4 de la original.
        ///     El shader lee 4x4 texels y los promedia
        /// </summary>
        private void scaleScene(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            var backBufferWidth = d3dDevice.PresentationParameters.BackBufferWidth;
            var backBufferHeight = d3dDevice.PresentationParameters.BackBufferHeight;
            var sampleOffsets = TgcPostProcessingUtils.computeDownScaleOffsets4x4(backBufferWidth, backBufferHeight);

            effect.Technique = "DownScale4x4";
            effect.SetValue("texSceneRT", sceneRT);
            effect.SetValue("sampleOffsets", TgcParserUtils.vector2ArrayToFloat2Array(sampleOffsets));
            var scaledSceneS = scaledSceneRT.GetSurfaceLevel(0);
            d3dDevice.SetRenderTarget(0, scaledSceneS);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            screenQuad.render(effect);

            d3dDevice.EndScene();

            scaledSceneS.Dispose();
        }

        /// <summary>
        ///     Recorremos toda la imagen de scaledSceneRT y buscamos el pixel con el maximo nivel de luminosidad.
        ///     Lo hacemos en GPU, con varias pasadas de downsampling, hasta llegar a una textura de 1x1.
        /// </summary>
        private void findAverageLuminance(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            //Obtener surfaces de todos los luminanceRTs
            var luminanceSurfaces = new Surface[luminanceRTs.Length];
            for (var i = 0; i < luminanceRTs.Length; i++)
            {
                luminanceSurfaces[i] = luminanceRTs[i].GetSurfaceLevel(0);
            }

            var curTexture = luminanceRTs.Length - 1;
            var surfWidth = luminanceSurfaces[curTexture].Description.Width;
            var surfHeight = luminanceSurfaces[curTexture].Description.Height;

            //Calcular los offsets de la primera pasada de downsampling de luminance
            float tU, tV;
            tU = 1.0f / (3.0f * surfWidth);
            tV = 1.0f / (3.0f * surfHeight);
            int x, y;
            var index = 0;
            var sampleOffsets = new Vector2[16];
            for (x = -1; x <= 1; x++)
            {
                for (y = -1; y <= 1; y++)
                {
                    sampleOffsets[index] = new Vector2(x * tU, y * tV);
                    index++;
                }
            }

            //Primera pasada de luminance: calculamos el promedio de log(luminance) de 4x4
            effect.Technique = "SampleAvgLuminance_Init";
            effect.SetValue("texSceneRT", scaledSceneRT);
            effect.SetValue("sampleOffsets", TgcParserUtils.vector2ArrayToFloat2Array(sampleOffsets));
            d3dDevice.SetRenderTarget(0, luminanceSurfaces[curTexture]);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            screenQuad.render(effect);
            curTexture--;

            //DEBUG
            //TextureLoader.Save(this.ShadersDir + "luminance_" + (curTexture+1) + ".bmp", ImageFileFormat.Bmp, luminanceRTs[curTexture + 1]);

            //Hacer pasadas de downsampling intermedio
            while (curTexture > 0)
            {
                //Calcular offsets de esta pasada de downsampling
                surfWidth = luminanceSurfaces[curTexture].Description.Width;
                surfHeight = luminanceSurfaces[curTexture].Description.Height;
                sampleOffsets = TgcPostProcessingUtils.computeDownScaleOffsets4x4(surfWidth, surfHeight);

                //Hacer downsampling
                effect.Technique = "SampleAvgLuminance_Intermediate";
                effect.SetValue("texSceneRT", luminanceRTs[curTexture + 1]);
                effect.SetValue("sampleOffsets", TgcParserUtils.vector2ArrayToFloat2Array(sampleOffsets));
                d3dDevice.SetRenderTarget(0, luminanceSurfaces[curTexture]);
                d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
                screenQuad.render(effect);
                curTexture--;

                //DEBUG
                //TextureLoader.Save(this.ShadersDir + "luminance_" + (curTexture+1) + ".bmp", ImageFileFormat.Bmp, luminanceRTs[curTexture + 1]);
            }

            //Hacer downsampling final de 1x1
            surfWidth = luminanceSurfaces[1].Description.Width;
            surfHeight = luminanceSurfaces[1].Description.Height;
            sampleOffsets = TgcPostProcessingUtils.computeDownScaleOffsets4x4(surfWidth, surfHeight);

            //Esta ultima pasada hace un promedio de 4x4 y luego hace exp para quitar el log y obtener el promedio de luminance de toda la escena
            effect.Technique = "SampleAvgLuminance_End";
            effect.SetValue("texSceneRT", luminanceRTs[1]);
            effect.SetValue("sampleOffsets", TgcParserUtils.vector2ArrayToFloat2Array(sampleOffsets));
            d3dDevice.SetRenderTarget(0, luminanceSurfaces[0]);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            screenQuad.render(effect);

            //DEBUG
            //TextureLoader.Save(this.ShadersDir + "luminance_" + 0 + ".bmp", ImageFileFormat.Bmp, luminanceRTs[0]);

            //Liberar todos los surfaces usados
            for (var i = 0; i < luminanceSurfaces.Length; i++)
            {
                luminanceSurfaces[i].Dispose();
            }

            d3dDevice.EndScene();
        }

        /// <summary>
        ///     Quedarse con los pixels de scaledSceneRT que superan un cierto nivel de brillo
        /// </summary>
        private void brightPass(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            var middleGray = (float)Modifiers["middleGray"];

            effect.Technique = "BrightPass";
            effect.SetValue("texSceneRT", scaledSceneRT);
            effect.SetValue("texLuminanceRT", luminanceRTs[0]);
            effect.SetValue("middleGray", middleGray);
            var brightPassS = brightPassRT.GetSurfaceLevel(0);
            d3dDevice.SetRenderTarget(0, brightPassS);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            screenQuad.render(effect);

            //DEBUG
            //TextureLoader.Save(this.ShadersDir + "brightPass.bmp", ImageFileFormat.Bmp, brightPassRT);

            brightPassS.Dispose();
            d3dDevice.EndScene();
        }

        /// <summary>
        ///     Hacer blur de bright-pass para generar efecto de bloom
        /// </summary>
        /// <param name="d3dDevice"></param>
        private void bloomPass(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            //Gaussian blur horizontal
            Vector2[] texCoordOffsets;
            float[] colorWeights;
            var bloomTempS = bloomTempRT.GetSurfaceLevel(0);
            TgcPostProcessingUtils.computeGaussianBlurSampleOffsets15(bloomTempS.Description.Width, 1, 1, true,
                out texCoordOffsets, out colorWeights);
            effect.Technique = "BloomPass";
            effect.SetValue("texBloomRT", brightPassRT);
            d3dDevice.SetRenderTarget(0, bloomTempS);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            screenQuad.render(effect);

            //Gaussian blur vertical
            TgcPostProcessingUtils.computeGaussianBlurSampleOffsets15(bloomTempS.Description.Height, 1, 1, false,
                out texCoordOffsets, out colorWeights);
            effect.Technique = "BloomPass";
            effect.SetValue("texBloomRT", bloomTempRT);
            effect.SetValue("gauss_offsets", TgcParserUtils.vector2ArrayToFloat2Array(texCoordOffsets));
            effect.SetValue("gauss_weights", colorWeights);
            var bloomS = bloomRT.GetSurfaceLevel(0);
            d3dDevice.SetRenderTarget(0, bloomS);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            screenQuad.render(effect);

            bloomTempS.Dispose();
            bloomS.Dispose();

            d3dDevice.EndScene();
        }

        /// <summary>
        ///     Render final donde une todo
        /// </summary>
        private void finalRender(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            var toneMapping = (bool)Modifiers["toneMapping"];
            effect.Technique = toneMapping ? "FinalRender" : "FinalRenderNoToneMapping";
            effect.SetValue("texSceneRT", sceneRT);
            effect.SetValue("texLuminanceRT", luminanceRTs[0]);
            effect.SetValue("texBloomRT", bloomRT);
            effect.SetValue("middleGray", (float)Modifiers["middleGray"]);
            d3dDevice.SetRenderTarget(0, pOldRT);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            screenQuad.render(effect);

            d3dDevice.EndScene();
        }

        public override void Dispose()
        {
            foreach (var m in meshes)
            {
                m.dispose();
            }
            effect.Dispose();
            screenQuad.dispose();
            sceneRT.Dispose();
            scaledSceneRT.Dispose();
            brightPassRT.Dispose();
            bloomRT.Dispose();
            bloomTempRT.Dispose();
            for (var i = 0; i < luminanceRTs.Length; i++)
            {
                luminanceRTs[i].Dispose();
            }
            pOldRT.Dispose();
        }
    }
}