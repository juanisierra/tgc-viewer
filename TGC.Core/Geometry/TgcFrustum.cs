using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using TGC.Core.Direct3D;
using TGC.Core.Shaders;
using TGC.Core.Textures;
using TGC.Core.Utils;

namespace TGC.Core.Geometry
{
    /// <summary>
    ///     Clase que representa el volumen del Frustum (vision actual).
    ///     Las normales de los planos del Frustum apuntan hacia adentro.
    ///     Tambien permite dibujar una malla debug del frustum
    ///     Solo puede ser invocado cuando se esta ejecutando un bloque de Render() de un TgcExample
    /// </summary>
    public class TgcFrustum
    {
        /// <summary>
        ///     Tipos de planos del Frustum
        /// </summary>
        public enum PlaneTypes
        {
            Left = 0,
            Right = 1,
            Top = 2,
            Bottom = 3,
            Near = 4,
            Far = 5
        }

        private static readonly Vector3 UP_VECTOR = new Vector3(0, 1, 0);

        private Color color;

        protected Effect effect;

        protected string technique;

        /// <summary>
        ///     VertexBuffer para mesh debug de Frustum
        /// </summary>
        private VertexBuffer vertexBuffer;

        public TgcFrustum()
        {
            FrustumPlanes = new Plane[6];

            color = Color.Green;
            AlphaBlendingValue = 0.7f;
        }

        /// <summary>
        ///     Permite acceder a una instancia de la clase TgcShaders desde cualquier parte del codigo.
        /// </summary>
        public static TgcFrustum Instance { get; } = new TgcFrustum();

        /// <summary>
        ///     Los 6 planos que componen el Frustum.
        ///     Estan en el siguiente orden:
        ///     Left, Right, Top, Bottom, Near, Far
        ///     Estan normalizados.
        ///     Sus normales hacia adentro.
        /// </summary>
        public Plane[] FrustumPlanes { get; } = new Plane[6];

        /// <summary>
        ///     Shader del mesh
        /// </summary>
        public Effect Effect
        {
            get { return effect; }
            set { effect = value; }
        }

        /// <summary>
        ///     Technique que se va a utilizar en el effect.
        ///     Cada vez que se llama a Render() se carga este Technique (pisando lo que el shader ya tenia seteado)
        /// </summary>
        public string Technique
        {
            get { return technique; }
            set { technique = value; }
        }

        /// <summary>
        ///     Left plane
        /// </summary>
        public Plane LeftPlane
        {
            get { return FrustumPlanes[(int)PlaneTypes.Left]; }
        }

        /// <summary>
        ///     Right plane
        /// </summary>
        public Plane RightPlane
        {
            get { return FrustumPlanes[(int)PlaneTypes.Right]; }
        }

        /// <summary>
        ///     Top plane
        /// </summary>
        public Plane TopPlane
        {
            get { return FrustumPlanes[(int)PlaneTypes.Top]; }
        }

        /// <summary>
        ///     Bottom plane
        /// </summary>
        public Plane BottomPlane
        {
            get { return FrustumPlanes[(int)PlaneTypes.Bottom]; }
        }

        /// <summary>
        ///     Near plane
        /// </summary>
        public Plane NearPlane
        {
            get { return FrustumPlanes[(int)PlaneTypes.Near]; }
        }

        /// <summary>
        ///     Far plane
        /// </summary>
        public Plane FarPlane
        {
            get { return FrustumPlanes[(int)PlaneTypes.Far]; }
        }

        /// <summary>
        ///     Transparencia (0, 1)
        /// </summary>
        public float AlphaBlendingValue { get; set; }

        /// <summary>
        ///     Color del mesh debug
        /// </summary>
        public Color Color
        {
            get { return color; }
            set { color = value; }
        }

        /// <summary>
        ///     Actualiza los planos que conforman el volumen del Frustum.
        ///     Los planos se calculan con las normales apuntando hacia adentro
        /// </summary>
        /// <param name="viewMatrix">View matrix</param>
        /// <param name="projectionMatrix">Projection matrix</param>
        public void updateVolume(Matrix viewMatrix, Matrix projectionMatrix)
        {
            var viewProjection = viewMatrix * projectionMatrix;

            //Left plane
            FrustumPlanes[0].A = viewProjection.M14 + viewProjection.M11;
            FrustumPlanes[0].B = viewProjection.M24 + viewProjection.M21;
            FrustumPlanes[0].C = viewProjection.M34 + viewProjection.M31;
            FrustumPlanes[0].D = viewProjection.M44 + viewProjection.M41;

            //Right plane
            FrustumPlanes[1].A = viewProjection.M14 - viewProjection.M11;
            FrustumPlanes[1].B = viewProjection.M24 - viewProjection.M21;
            FrustumPlanes[1].C = viewProjection.M34 - viewProjection.M31;
            FrustumPlanes[1].D = viewProjection.M44 - viewProjection.M41;

            //Top plane
            FrustumPlanes[2].A = viewProjection.M14 - viewProjection.M12;
            FrustumPlanes[2].B = viewProjection.M24 - viewProjection.M22;
            FrustumPlanes[2].C = viewProjection.M34 - viewProjection.M32;
            FrustumPlanes[2].D = viewProjection.M44 - viewProjection.M42;

            //Bottom plane
            FrustumPlanes[3].A = viewProjection.M14 + viewProjection.M12;
            FrustumPlanes[3].B = viewProjection.M24 + viewProjection.M22;
            FrustumPlanes[3].C = viewProjection.M34 + viewProjection.M32;
            FrustumPlanes[3].D = viewProjection.M44 + viewProjection.M42;

            //Near plane
            FrustumPlanes[4].A = viewProjection.M13;
            FrustumPlanes[4].B = viewProjection.M23;
            FrustumPlanes[4].C = viewProjection.M33;
            FrustumPlanes[4].D = viewProjection.M43;

            //Far plane
            FrustumPlanes[5].A = viewProjection.M14 - viewProjection.M13;
            FrustumPlanes[5].B = viewProjection.M24 - viewProjection.M23;
            FrustumPlanes[5].C = viewProjection.M34 - viewProjection.M33;
            FrustumPlanes[5].D = viewProjection.M44 - viewProjection.M43;

            //Normalize planes
            for (var i = 0; i < 6; i++)
            {
                FrustumPlanes[i] = Plane.Normalize(FrustumPlanes[i]);
            }
        }

        /// <summary>
        ///     Calcular los 8 vertices del Frustum
        ///     Basado en: http://www.lighthouse3d.com/tutorials/view-frustum-culling/geometric-approach-implementation/
        /// </summary>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="aspectRatio"></param>
        /// <param name="nearDistance"></param>
        /// <param name="farDistance"></param>
        /// <param name="fieldOfViewY"></param>
        /// <returns>Los 8 vertices del Frustum</returns>
        private Vector3[] computeFrustumCorners(Vector3 position, Vector3 lookAt, float aspectRatio, float nearDistance,
            float farDistance, float fieldOfViewY)
        {
            var corners = new Vector3[8];
            /*
             (ntl)0 ---- 1(ntr)
                  |      |   Near-face
             (nbl)2 ---- 3(nbr)

             (ftl)4 ---- 5(ftr)
                  |      |   Far-face
             (fbl)6 ---- 7(fbr)
             */

            var tang = FastMath.Tan(fieldOfViewY * 0.5f);
            var nh = nearDistance * tang;
            var nw = nh * aspectRatio;
            var fh = farDistance * tang;
            var fw = fh * aspectRatio;

            // compute the Z axis of camera
            // this axis points in the opposite direction from
            // the looking direction
            var Z = Vector3.Subtract(position, lookAt);
            Z.Normalize();

            // X axis of camera with given "up" vector and Z axis
            var X = Vector3.Cross(UP_VECTOR, Z);
            X.Normalize();

            // the real "up" vector is the cross product of Z and X
            var Y = Vector3.Cross(Z, X);

            // compute the centers of the near and far planes
            var nc = position - Z * nearDistance;
            var fc = position - Z * farDistance;

            // compute the 4 corners of the frustum on the near plane
            corners[0] = nc + Y * nh - X * nw; //ntl
            corners[1] = nc + Y * nh + X * nw; //ntr
            corners[2] = nc - Y * nh - X * nw; //nbl
            corners[3] = nc - Y * nh + X * nw; //nbr

            // compute the 4 corners of the frustum on the far plane
            corners[4] = fc + Y * fh - X * fw; //ftl
            corners[5] = fc + Y * fh + X * fw; //ftr
            corners[6] = fc - Y * fh - X * fw; //fbl
            corners[7] = fc - Y * fh + X * fw; //fbr

            return corners;
        }

        /// <summary>
        ///     Actualizar el mesh para debug del Frustum
        /// </summary>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        public void updateMesh(Vector3 position, Vector3 lookAt)
        {
            updateMesh(position, lookAt, D3DDevice.Instance.AspectRatio, D3DDevice.Instance.ZNearPlaneDistance,
                D3DDevice.Instance.ZFarPlaneDistance, D3DDevice.Instance.FieldOfView);
        }

        /// <summary>
        ///     Actualizar el mesh para debug del Frustum
        ///     Basado en: http://zach.in.tu-clausthal.de/teaching/cg_literatur/lighthouse3d_view_frustum_culling/index.html
        /// </summary>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="aspectRatio"></param>
        /// <param name="nearDistance"></param>
        /// <param name="farDistance"></param>
        /// <param name="fieldOfViewY"></param>
        public void updateMesh(Vector3 position, Vector3 lookAt, float aspectRatio, float nearDistance,
            float farDistance, float fieldOfViewY)
        {
            //Calcular los 8 vertices extremos
            var corners = computeFrustumCorners(position, lookAt, aspectRatio, nearDistance, farDistance, fieldOfViewY);

            //Crear vertexBuffer
            if (vertexBuffer == null)
            {
                vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionColored), 36, D3DDevice.Instance.Device,
                    Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionColored.Format, Pool.Default);
            }

            //Cargar vertices de las 6 caras
            var vertices = new CustomVertex.PositionColored[36];
            var color = this.color.ToArgb();

            // Front face
            vertices[0] = new CustomVertex.PositionColored(corners[0], color);
            vertices[1] = new CustomVertex.PositionColored(corners[2], color);
            vertices[2] = new CustomVertex.PositionColored(corners[3], color);
            vertices[3] = new CustomVertex.PositionColored(corners[0], color);
            vertices[4] = new CustomVertex.PositionColored(corners[3], color);
            vertices[5] = new CustomVertex.PositionColored(corners[2], color);

            // Back face
            vertices[6] = new CustomVertex.PositionColored(corners[5], color);
            vertices[7] = new CustomVertex.PositionColored(corners[4], color);
            vertices[8] = new CustomVertex.PositionColored(corners[6], color);
            vertices[9] = new CustomVertex.PositionColored(corners[6], color);
            vertices[10] = new CustomVertex.PositionColored(corners[7], color);
            vertices[11] = new CustomVertex.PositionColored(corners[5], color);

            // Top face
            vertices[12] = new CustomVertex.PositionColored(corners[4], color);
            vertices[13] = new CustomVertex.PositionColored(corners[5], color);
            vertices[14] = new CustomVertex.PositionColored(corners[1], color);
            vertices[15] = new CustomVertex.PositionColored(corners[4], color);
            vertices[16] = new CustomVertex.PositionColored(corners[1], color);
            vertices[17] = new CustomVertex.PositionColored(corners[0], color);

            // Bottom face
            vertices[18] = new CustomVertex.PositionColored(corners[2], color);
            vertices[19] = new CustomVertex.PositionColored(corners[3], color);
            vertices[20] = new CustomVertex.PositionColored(corners[7], color);
            vertices[21] = new CustomVertex.PositionColored(corners[2], color);
            vertices[22] = new CustomVertex.PositionColored(corners[7], color);
            vertices[23] = new CustomVertex.PositionColored(corners[6], color);

            // Left face
            vertices[24] = new CustomVertex.PositionColored(corners[4], color);
            vertices[25] = new CustomVertex.PositionColored(corners[0], color);
            vertices[26] = new CustomVertex.PositionColored(corners[2], color);
            vertices[27] = new CustomVertex.PositionColored(corners[4], color);
            vertices[28] = new CustomVertex.PositionColored(corners[2], color);
            vertices[29] = new CustomVertex.PositionColored(corners[6], color);

            // Right face
            vertices[30] = new CustomVertex.PositionColored(corners[1], color);
            vertices[31] = new CustomVertex.PositionColored(corners[5], color);
            vertices[32] = new CustomVertex.PositionColored(corners[7], color);
            vertices[33] = new CustomVertex.PositionColored(corners[1], color);
            vertices[34] = new CustomVertex.PositionColored(corners[7], color);
            vertices[35] = new CustomVertex.PositionColored(corners[3], color);

            //Actualizar vertexBuffer
            vertexBuffer.SetData(vertices, 0, LockFlags.None);
            vertices = null;
        }

        /// <summary>
        ///     Dibujar mesh debug del Frustum.
        ///     Antes se debe llamar a updateMesh()
        ///     Setear el effect para el shader antes
        /// </summary>
        public void render()
        {
            TexturesManager.Instance.clear(0);
            TexturesManager.Instance.clear(1);

            //Cargar shader si es la primera vez
            if (effect == null)
            {
                effect = TgcShaders.Instance.VariosShader;
                technique = TgcShaders.T_POSITION_COLORED_ALPHA;
            }

            TgcShaders.Instance.setShaderMatrixIdentity(effect);
            D3DDevice.Instance.Device.VertexDeclaration = TgcShaders.Instance.VdecPositionColored;
            effect.Technique = technique;
            D3DDevice.Instance.Device.SetStreamSource(0, vertexBuffer, 0);

            //transparencia
            effect.SetValue("alphaValue", AlphaBlendingValue);
            D3DDevice.Instance.Device.RenderState.AlphaTestEnable = true;
            D3DDevice.Instance.Device.RenderState.AlphaBlendEnable = true;

            //Draw shader
            effect.Begin(0);
            effect.BeginPass(0);
            D3DDevice.Instance.Device.DrawPrimitives(PrimitiveType.TriangleList, 0, 12);
            effect.EndPass();
            effect.End();

            D3DDevice.Instance.Device.RenderState.AlphaTestEnable = false;
            D3DDevice.Instance.Device.RenderState.AlphaBlendEnable = false;
        }

        /// <summary>
        ///     Liberar recursos
        /// </summary>
        public void dispose()
        {
            vertexBuffer.Dispose();
        }
    }
}