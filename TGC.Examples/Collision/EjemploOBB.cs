using Microsoft.DirectX;
using TGC.Core;
using TGC.Core.Camara;
using TGC.Core.Example;
using TGC.Core.Geometry;
using TGC.Core.SceneLoader;
using TGC.Core.UserControls;
using TGC.Core.UserControls.Modifier;
using TGC.Core.Utils;

namespace TGC.Examples.Collision
{
    /// <summary>
    ///     Ejemplo EjemploOBB:
    ///     Unidades Involucradas:
    ///     # Unidad 6 - Detecci�n de Colisiones - Oriented BoundingBox (OBB)
    ///     Muestra como crear un Oriented BoundingBox a partir de un mesh.
    ///     El mesh se puede rotar el OBB acompa�a esta rotacion (cosa que el AABB no puede hacer)
    ///     Autor: Mat�as Leone, Leandro Barbagallo
    /// </summary>
    public class EjemploOBB : TgcExample
    {
        private TgcMesh mesh;
        private TgcObb obb;

        public EjemploOBB(string mediaDir, string shadersDir, TgcUserVars userVars, TgcModifiers modifiers,
            TgcAxisLines axisLines, TgcCamera camara)
            : base(mediaDir, shadersDir, userVars, modifiers, axisLines, camara)
        {
            Category = "Collision";
            Name = "OBB";
            Description = "Muestra como crear un Oriented BoundingBox a partir de un mesh. Movimiento con mouse.";
        }

        public override void Init()
        {
            //Cargar modelo
            var loader = new TgcSceneLoader();
            var scene =
                loader.loadSceneFromFile(MediaDir +
                                         "MeshCreator\\Meshes\\Vehiculos\\StarWars-ATST\\StarWars-ATST-TgcScene.xml");
            mesh = scene.Meshes[0];

            //Computar OBB a partir del AABB del mesh. Inicialmente genera el mismo volumen que el AABB, pero luego te permite rotarlo (cosa que el AABB no puede)
            obb = TgcObb.computeFromAABB(mesh.BoundingBox);

            //Otra alternativa es computar OBB a partir de sus vertices. Esto genera un OBB lo mas apretado posible pero es una operacion costosa
            //obb = TgcObb.computeFromPoints(mesh.getVertexPositions());

            //Alejar camara rotacional segun tama�o del BoundingBox del objeto
            Camara = new TgcRotationalCamera(mesh.BoundingBox.calculateBoxCenter(),
                mesh.BoundingBox.calculateBoxRadius() * 2);

            //Modifier para poder rotar y mover el mesh
            Modifiers.addFloat("rotation", 0, 360, 0);
            Modifiers.addVertex3f("position", new Vector3(0, 0, 0), new Vector3(50, 50, 50), new Vector3(0, 0, 0));
        }

        public override void Update()
        {
            PreUpdate();
        }

        public override void Render()
        {
            PreRender();

            //Obtener rotacion de mesh (pasar a radianes)
            var rotation = FastMath.ToRad((float)Modifiers["rotation"]);

            //Rotar mesh y rotar OBB. A diferencia del AABB, nosotros tenemos que mantener el OBB actualizado segun cada movimiento del mesh
            var lastRot = mesh.Rotation;
            var rotationDiff = rotation - lastRot.Y;
            mesh.rotateY(rotationDiff);
            obb.rotate(new Vector3(0, rotationDiff, 0));

            //Actualizar posicion
            var position = (Vector3)Modifiers["position"];
            var lastPos = mesh.Position;
            var posDiff = position - lastPos;
            mesh.move(posDiff);
            obb.move(posDiff);

            //Renderizar modelo
            mesh.render();

            //Renderizar obb
            obb.render();

            PostRender();
        }

        public override void Dispose()
        {
            mesh.dispose();
            obb.dispose();
        }
    }
}