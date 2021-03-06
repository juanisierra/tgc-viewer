using Microsoft.DirectX;
using System.Drawing;
using TGC.Core;
using TGC.Core.Camara;
using TGC.Core.Direct3D;
using TGC.Core.Example;
using TGC.Core.Geometry;
using TGC.Core.Textures;
using TGC.Core.UserControls;
using TGC.Core.UserControls.Modifier;
using TGC.Core.Utils;

namespace TGC.Examples.GeometryBasics
{
    /// <summary>
    ///     Ejemplo en Blanco. Ideal para copiar y pegar cuando queres empezar a hacer tu propio ejemplo.
    /// </summary>
    public class Cilindro : TgcExample
    {
        private string currentTexture;
        private TgcCylinder cylinder;

        public Cilindro(string mediaDir, string shadersDir, TgcUserVars userVars, TgcModifiers modifiers,
            TgcAxisLines axisLines, TgcCamera camara)
            : base(mediaDir, shadersDir, userVars, modifiers, axisLines, camara)
        {
            Category = "GeometryBasics";
            Name = "Crear Cilindro 3D";
            Description =
                "Muestra como crear un cilindro 3D con la herramienta TgcCylinder, cuyos parámetros pueden ser modificados. Movimiento con mouse.";
        }

        public override void Init()
        {
            cylinder = new TgcCylinder(new Vector3(0, 0, 0), 2, 4);

            //cylinder.Transform = Matrix.Scaling(2, 1, 1) * Matrix.RotationYawPitchRoll(0, 0, 1);
            //cylinder.AutoTransformEnable = false;
            //cylinder.updateValues();

            cylinder.AlphaBlendEnable = true;

            Modifiers.addBoolean("boundingCylinder", "boundingCylinder", false);
            Modifiers.addColor("color", Color.White);
            Modifiers.addInt("alpha", 0, 255, 255);
            Modifiers.addTexture("texture", MediaDir + "\\Texturas\\madera.jpg");
            Modifiers.addBoolean("useTexture", "useTexture", true);

            Modifiers.addVertex3f("size", new Vector3(-3, -3, 1), new Vector3(7, 7, 10), new Vector3(2, 2, 5));
            Modifiers.addVertex3f("position", new Vector3(-20, -20, -20), new Vector3(20, 20, 20), new Vector3(0, 0, 0));
            var angle = FastMath.TWO_PI;
            Modifiers.addVertex3f("rotation", new Vector3(-angle, -angle, -angle), new Vector3(angle, angle, angle),
                new Vector3(0, 0, 0));
        }

        public override void Update()
        {
            PreUpdate();
        }

        public override void Render()
        {
            PreRender();

            var modifiers = Modifiers;
            var size = (Vector3)modifiers.getValue("size");
            var position = (Vector3)modifiers.getValue("position");
            var rotation = (Vector3)modifiers.getValue("rotation");

            var texturePath = (string)modifiers.getValue("texture");
            if (texturePath != currentTexture)
            {
                currentTexture = texturePath;
                cylinder.setTexture(TgcTexture.createTexture(D3DDevice.Instance.Device, currentTexture));
            }

            cylinder.UseTexture = (bool)modifiers.getValue("useTexture");

            cylinder.Position = position;
            cylinder.Rotation = rotation;
            cylinder.TopRadius = size.X;
            cylinder.BottomRadius = size.Y;
            cylinder.Length = size.Z;

            var alpha = (int)modifiers.getValue("alpha");
            var color = (Color)modifiers.getValue("color");
            cylinder.Color = Color.FromArgb(alpha, color);

            cylinder.updateValues();

            if ((bool)modifiers.getValue("boundingCylinder"))
                cylinder.BoundingCylinder.render();
            else
                cylinder.render();

            PostRender();
        }

        public override void Dispose()
        {
            cylinder.dispose();
        }
    }
}