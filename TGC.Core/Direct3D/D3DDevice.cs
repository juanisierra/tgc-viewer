﻿using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using TGC.Core.Textures;
using TGC.Core.Utils;

namespace TGC.Core.Direct3D
{
    public class D3DDevice
    {
        public static readonly Material DEFAULT_MATERIAL = new Material();

        /// <summary>
        ///     Constructor privado para poder hacer el singleton
        /// </summary>
        private D3DDevice()
        {
        }

        /// <summary>
        ///     Device de DirectX 3D para crear primitivas
        /// </summary>
        public Device Device { get; set; }

        //Valores de configuracion de la matriz de Proyeccion
        public float FieldOfView { get; set; } = FastMath.ToRad(45.0f);

        public float AspectRatio { get; set; } = -1f;

        public float ZFarPlaneDistance { get; set; } = 10000f;
        public float ZNearPlaneDistance { get; set; } = 1f;
        public bool ParticlesEnabled { get; set; } = false;

        public static D3DDevice Instance { get; } = new D3DDevice();

        public int Width { get; set; }

        public int Height { get; set; }

        /// <summary>
        ///     Valores default del Direct3d Device
        /// </summary>
        public void DefaultValues()
        {
            //Frustum values
            Device.Transform.Projection = Matrix.PerspectiveFovLH(FieldOfView, AspectRatio, ZNearPlaneDistance, ZFarPlaneDistance);

            //Render state
            Device.RenderState.SpecularEnable = false;
            Device.RenderState.FillMode = FillMode.Solid;
            Device.RenderState.CullMode = Cull.None;
            Device.RenderState.ShadeMode = ShadeMode.Gouraud;
            Device.RenderState.MultiSampleAntiAlias = true;
            Device.RenderState.SlopeScaleDepthBias = -0.1f;
            Device.RenderState.DepthBias = 0f;
            Device.RenderState.ColorVertex = true;
            Device.RenderState.Lighting = false;
            Device.RenderState.ZBufferEnable = true;
            Device.RenderState.FogEnable = false;

            //Alpha Blending
            Device.RenderState.AlphaBlendEnable = false;
            Device.RenderState.AlphaTestEnable = false;
            Device.RenderState.ReferenceAlpha = 50; //verificar un valor optimo.
            Device.RenderState.AlphaFunction = Compare.Greater;
            Device.RenderState.BlendOperation = BlendOperation.Add;
            Device.RenderState.SourceBlend = Blend.SourceAlpha;
            Device.RenderState.DestinationBlend = Blend.InvSourceAlpha;

            //Texture Filtering
            Device.SetSamplerState(0, SamplerStageStates.MinFilter, (int)TextureFilter.Linear);
            Device.SetSamplerState(0, SamplerStageStates.MagFilter, (int)TextureFilter.Linear);
            Device.SetSamplerState(0, SamplerStageStates.MipFilter, (int)TextureFilter.Linear);

            Device.SetSamplerState(1, SamplerStageStates.MinFilter, (int)TextureFilter.Linear);
            Device.SetSamplerState(1, SamplerStageStates.MagFilter, (int)TextureFilter.Linear);
            Device.SetSamplerState(1, SamplerStageStates.MipFilter, (int)TextureFilter.Linear);

            //Clear lights
            foreach (Light light in Device.Lights)
            {
                light.Enabled = false;
            }

            //Limpiar todas las texturas
            TexturesManager.Instance.clearAll();

            //Reset Material
            Device.Material = DEFAULT_MATERIAL;

            //Limpiar IndexBuffer
            Device.Indices = null;

            enableParticles();
        }

        /// <summary>
        ///     habilita los points sprites.
        ///     Estaba este comentario antes, asi que lo dejo con default false.
        ///     INEXPLICABLE PERO ESTO HACE QUE MI NOTEBOOK SE CUELGUE CON LA PANTALLA EN NEGRO!!!!!!!!!!
        /// </summary>
        public void enableParticles()
        {
            if (ParticlesEnabled)
            {
                //PointSprite
                Device.RenderState.PointSpriteEnable = true;
                Device.RenderState.PointScaleEnable = true;
                Device.RenderState.PointScaleA = 1.0f;
                Device.RenderState.PointScaleB = 1.0f;
                Device.RenderState.PointScaleC = 0.0f;
            }
        }

        public void InitializeD3DDevice(Panel panel)
        {
            AspectRatio = (float)panel.Width / panel.Height;
            Width = panel.Width;
            Height = panel.Height;

            var caps = Manager.GetDeviceCaps(Manager.Adapters.Default.Adapter, DeviceType.Hardware);
            Debug.WriteLine("Max primitive count:" + caps.MaxPrimitiveCount);

            CreateFlags flags;
            if (caps.DeviceCaps.SupportsHardwareTransformAndLight)
                flags = CreateFlags.HardwareVertexProcessing;
            else
                flags = CreateFlags.SoftwareVertexProcessing;

            var d3dpp = new PresentParameters();

            d3dpp.BackBufferFormat = Format.Unknown;
            d3dpp.SwapEffect = SwapEffect.Discard;
            d3dpp.Windowed = true;
            d3dpp.EnableAutoDepthStencil = true;
            d3dpp.AutoDepthStencilFormat = DepthFormat.D24S8;
            d3dpp.PresentationInterval = PresentInterval.Immediate;

            //Antialiasing
            if (Manager.CheckDeviceMultiSampleType(Manager.Adapters.Default.Adapter, DeviceType.Hardware,
                Manager.Adapters.Default.CurrentDisplayMode.Format, true, MultiSampleType.NonMaskable))
            {
                d3dpp.MultiSample = MultiSampleType.NonMaskable;
                d3dpp.MultiSampleQuality = 0;
            }
            else
            {
                d3dpp.MultiSample = MultiSampleType.None;
            }

            //Crear Graphics Device
            Device.IsUsingEventHandlers = false;
            var d3DDevice = new Device(0, DeviceType.Hardware, panel, flags, d3dpp);

            Device = d3DDevice;

            Device.DeviceReset += OnResetDevice;
            OnResetDevice(Device, null);
        }

        public void FillModeWireFrame()
        {
            Device.RenderState.FillMode = FillMode.WireFrame;
        }

        public void FillModeWireSolid()
        {
            Device.RenderState.FillMode = FillMode.Solid;
        }

        /// <summary>
        ///     This event-handler is a good place to create and initialize any
        ///     Direct3D related objects, which may become invalid during a
        ///     device reset.
        /// </summary>
        public void OnResetDevice(object sender, EventArgs e)
        {
            //TODO antes hacia esto que no entiendo porque GuiController.Instance.onResetDevice();
            //ese metodo se movio a Decice, pero solo detenia el ejemplo ejecutaba doResetDevice y lo volvia a cargar...
            DoResetDevice();
        }

        /// <summary>
        ///     Hace las operaciones de Reset del device
        /// </summary>
        public void DoResetDevice()
        {
            DefaultValues();

            //Reset Timer
            HighResolutionTimer.Instance.Reset();
        }

        public void Dispose()
        {
            Device.Dispose();
        }
    }
}