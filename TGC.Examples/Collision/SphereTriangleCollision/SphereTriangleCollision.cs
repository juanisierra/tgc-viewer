using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX.DirectInput;
using System.Collections.Generic;
using System.Drawing;
using TGC.Core;
using TGC.Core.Camara;
using TGC.Core.Direct3D;
using TGC.Core.Example;
using TGC.Core.Geometry;
using TGC.Core.Input;
using TGC.Core.SceneLoader;
using TGC.Core.SkeletalAnimation;
using TGC.Core.Terrain;
using TGC.Core.Textures;
using TGC.Core.UserControls;
using TGC.Core.UserControls.Modifier;
using TGC.Core.Utils;

namespace TGC.Examples.Collision.SphereTriangleCollision
{
    /// <summary>
    ///     Ejemplo SphereCollision
    ///     Unidades Involucradas:
    ///     # Unidad 4 - Texturas e Iluminaci�n - SkyBox
    ///     # Unidad 6 - Detecci�n de Colisiones - Estrategia Integral
    ///     Ejemplo de nivel avanzado.
    ///     Se recomienda leer primero EjemploColisionesThirdPerson que posee el mismo espiritu de ejemplo pero mas sencillo.
    ///     Muestra una posible implementaci�n de la Estrategia Integral de colisiones de una esfera con gravedad y sliding,
    ///     explicada en el apunte de Detecci�n de Colisiones.
    ///     Utiliza la clase SphereCollisionManager para encapsular la estrategia de colisi�n. Esta clase se basa
    ///     en el paper: http://www.peroxide.dk/papers/collision/collision.pdf.
    ///     El paper no ha sido implementado en su totalidad y a�n existen muchos puntos por mejorar.
    ///     Autor: Mat�as Leone, Leandro Barbagallo
    /// </summary>
    public class SphereTriangleCollision : TgcExample
    {
        private readonly List<Collider> objetosColisionables = new List<Collider>();
        private TgcThirdPersonCamera camaraInterna;
        private TgcBoundingSphere characterSphere;
        private SphereTriangleCollisionManager collisionManager;
        private TgcArrow collisionNormalArrow;
        private TgcBox collisionPoint;
        private TgcArrow directionArrow;
        private TgcScene escenario;
        private bool jumping;
        private float jumpingElapsedTime;
        private TgcSkeletalMesh personaje;
        private TgcSkyBox skyBox;

        public SphereTriangleCollision(string mediaDir, string shadersDir, TgcUserVars userVars, TgcModifiers modifiers,
            TgcAxisLines axisLines, TgcCamera camara)
            : base(mediaDir, shadersDir, userVars, modifiers, axisLines, camara)
        {
            Category = "Collision";
            Name = "Colision Esfera-Triangulos";
            Description =
                "Estrategia integral de colisi�n: BoundingSphere contra tri�ngulos + Gravedad + Sliding + Jump. Movimiento con W, A, S, D, Space.";
        }

        public override void Init()
        {
            //Cargar escenario espec�fico para este ejemplo
            var loader = new TgcSceneLoader();
            escenario = loader.loadSceneFromFile(MediaDir + "\\MeshCreator\\Scenes\\Mountains\\Mountains-TgcScene.xml");

            //Cargar personaje con animaciones
            var skeletalLoader = new TgcSkeletalLoader();
            personaje =
                skeletalLoader.loadMeshAndAnimationsFromFile(
                    MediaDir + "SkeletalAnimations\\Robot\\Robot-TgcSkeletalMesh.xml",
                    MediaDir + "SkeletalAnimations\\Robot\\",
                    new[]
                    {
                        MediaDir + "SkeletalAnimations\\Robot\\Caminando-TgcSkeletalAnim.xml",
                        MediaDir + "SkeletalAnimations\\Robot\\Parado-TgcSkeletalAnim.xml"
                    });

            //Le cambiamos la textura para diferenciarlo un poco
            personaje.changeDiffuseMaps(new[]
            {
                TgcTexture.createTexture(D3DDevice.Instance.Device,
                    MediaDir + "SkeletalAnimations\\Robot\\Textures\\uvwGreen.jpg")
            });

            //Configurar animacion inicial
            personaje.playAnimation("Parado", true);
            //Escalarlo porque es muy grande
            personaje.Position = new Vector3(0, 2500, -150);
            //Rotarlo 180� porque esta mirando para el otro lado
            personaje.rotateY(Geometry.DegreeToRadian(180f));

            //BoundingSphere que va a usar el personaje
            personaje.AutoUpdateBoundingBox = false;
            characterSphere = new TgcBoundingSphere(personaje.BoundingBox.calculateBoxCenter(),
                personaje.BoundingBox.calculateBoxRadius());
            jumping = false;

            //Almacenar volumenes de colision del escenario
            objetosColisionables.Clear();
            foreach (var mesh in escenario.Meshes)
            {
                //Los objetos del layer "TriangleCollision" son colisiones a nivel de triangulo
                if (mesh.Layer == "TriangleCollision")
                {
                    objetosColisionables.Add(TriangleMeshCollider.fromMesh(mesh));
                }
                //El resto de los objetos son colisiones de BoundingBox
                else
                {
                    objetosColisionables.Add(BoundingBoxCollider.fromBoundingBox(mesh.BoundingBox));
                }
            }

            //Crear linea para mostrar la direccion del movimiento del personaje
            directionArrow = new TgcArrow();
            directionArrow.BodyColor = Color.Red;
            directionArrow.HeadColor = Color.Green;
            directionArrow.Thickness = 1;
            directionArrow.HeadSize = new Vector2(10, 20);

            //Linea para normal de colision
            collisionNormalArrow = new TgcArrow();
            collisionNormalArrow.BodyColor = Color.Blue;
            collisionNormalArrow.HeadColor = Color.Yellow;
            collisionNormalArrow.Thickness = 1;
            collisionNormalArrow.HeadSize = new Vector2(5, 10);

            //Caja para marcar punto de colision
            collisionPoint = TgcBox.fromSize(new Vector3(20, 20, 20), Color.Red);

            //Crear manejador de colisiones
            collisionManager = new SphereTriangleCollisionManager();
            collisionManager.GravityEnabled = true;

            //Configurar camara en Tercer Persona
            camaraInterna = new TgcThirdPersonCamera(personaje.Position, new Vector3(0, 100, 0), 100, -400);
            Camara = camaraInterna;

            //Crear SkyBox
            skyBox = new TgcSkyBox();
            skyBox.Center = new Vector3(0, 0, 0);
            skyBox.Size = new Vector3(10000, 10000, 10000);
            var texturesPath = MediaDir + "Texturas\\Quake\\SkyBox3\\";
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Up, texturesPath + "Up.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Down, texturesPath + "Down.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Left, texturesPath + "Left.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Right, texturesPath + "Right.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Front, texturesPath + "Back.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Back, texturesPath + "Front.jpg");
            skyBox.InitSkyBox();

            //Modifier para ver BoundingBox
            Modifiers.addBoolean("Collisions", "Collisions", true);
            Modifiers.addBoolean("showBoundingBox", "Bouding Box", true);

            //Modifiers para desplazamiento del personaje
            Modifiers.addFloat("VelocidadCaminar", 0, 50, 10);
            Modifiers.addFloat("VelocidadRotacion", 1f, 360f, 150f);
            Modifiers.addBoolean("HabilitarGravedad", "Habilitar Gravedad", true);
            Modifiers.addVertex3f("Gravedad", new Vector3(-50, -50, -50), new Vector3(50, 50, 50),
                new Vector3(0, -24, 0));
            Modifiers.addFloat("SlideFactor", 0f, 2f, 1f);
            Modifiers.addFloat("Pendiente", 0f, 1f, 0.7f);
            Modifiers.addFloat("VelocidadSalto", 0f, 100f, 20f);
            Modifiers.addFloat("TiempoSalto", 0f, 2f, 0.5f);

            UserVars.addVar("Movement");
            UserVars.addVar("ySign");
        }

        public override void Update()
        {
            PreUpdate();
        }

        public override void Render()
        {
            PreRender();

            //Obtener boolean para saber si hay que mostrar Bounding Box
            var showBB = (bool)Modifiers.getValue("showBoundingBox");

            //obtener velocidades de Modifiers
            var velocidadCaminar = (float)Modifiers.getValue("VelocidadCaminar");
            var velocidadRotacion = (float)Modifiers.getValue("VelocidadRotacion");
            var velocidadSalto = (float)Modifiers.getValue("VelocidadSalto");
            var tiempoSalto = (float)Modifiers.getValue("TiempoSalto");

            //Calcular proxima posicion de personaje segun Input
            var moveForward = 0f;
            float rotate = 0;
            var d3dInput = TgcD3dInput.Instance;
            var moving = false;
            var rotating = false;
            float jump = 0;

            //Adelante
            if (d3dInput.keyDown(Key.W))
            {
                moveForward = -velocidadCaminar;
                moving = true;
            }

            //Atras
            if (d3dInput.keyDown(Key.S))
            {
                moveForward = velocidadCaminar;
                moving = true;
            }

            //Derecha
            if (d3dInput.keyDown(Key.D))
            {
                rotate = velocidadRotacion;
                rotating = true;
            }

            //Izquierda
            if (d3dInput.keyDown(Key.A))
            {
                rotate = -velocidadRotacion;
                rotating = true;
            }

            //Jump
            if (!jumping && d3dInput.keyPressed(Key.Space))
            {
                if (collisionManager.Collision)
                {
                    jumping = true;
                    jumpingElapsedTime = 0f;
                    jump = 0;
                }
            }

            //Si hubo rotacion
            if (rotating)
            {
                //Rotar personaje y la camara, hay que multiplicarlo por el tiempo transcurrido para no atarse a la velocidad el hardware
                var rotAngle = Geometry.DegreeToRadian(rotate * ElapsedTime);
                personaje.rotateY(rotAngle);
                camaraInterna.rotateY(rotAngle);
            }

            //Si hubo desplazamiento
            if (moving)
            {
                //Activar animacion de caminando
                personaje.playAnimation("Caminando", true);
            }

            //Si no se esta moviendo, activar animacion de Parado
            else
            {
                personaje.playAnimation("Parado", true);
            }

            //Actualizar salto
            if (jumping)
            {
                jumpingElapsedTime += ElapsedTime;
                if (jumpingElapsedTime > tiempoSalto)
                {
                    jumping = false;
                }
                else
                {
                    jump = velocidadSalto;
                }
            }

            //Vector de movimiento
            var movementVector = Vector3.Empty;
            if (moving || jumping)
            {
                //Aplicar movimiento, desplazarse en base a la rotacion actual del personaje
                //jump *= elapsedTime;
                movementVector = new Vector3(
                    FastMath.Sin(personaje.Rotation.Y) * moveForward,
                    jump,
                    FastMath.Cos(personaje.Rotation.Y) * moveForward
                    );
            }

            //Actualizar valores de gravedad
            collisionManager.GravityEnabled = (bool)Modifiers["HabilitarGravedad"];
            collisionManager.GravityForce = (Vector3)Modifiers["Gravedad"] /** elapsedTime*/;
            collisionManager.SlideFactor = (float)Modifiers["SlideFactor"];
            collisionManager.OnGroundMinDotValue = (float)Modifiers["Pendiente"];

            //Si esta saltando, desactivar gravedad
            if (jumping)
            {
                collisionManager.GravityEnabled = false;
            }

            //Mover personaje con detecci�n de colisiones, sliding y gravedad
            if ((bool)Modifiers["Collisions"])
            {
                var realMovement = collisionManager.moveCharacter(characterSphere, movementVector, objetosColisionables);
                personaje.move(realMovement);

                //Cargar desplazamiento realizar en UserVar
                UserVars.setValue("Movement", TgcParserUtils.printVector3(realMovement));
                UserVars.setValue("ySign", realMovement.Y);
            }
            else
            {
                personaje.move(movementVector);
            }

            //Si estaba saltando y hubo colision de una superficie que mira hacia abajo, desactivar salto
            if (jumping && collisionManager.Collision)
            {
                jumping = false;
            }

            //Hacer que la camara siga al personaje en su nueva posicion
            camaraInterna.Target = personaje.Position;

            //Actualizar valores de la linea de movimiento
            directionArrow.PStart = characterSphere.Center;
            directionArrow.PEnd = characterSphere.Center + Vector3.Multiply(movementVector, 50);
            directionArrow.updateValues();

            //Actualizar valores de normal de colision
            if (collisionManager.Collision)
            {
                collisionNormalArrow.PStart = collisionManager.LastCollisionPoint;
                collisionNormalArrow.PEnd = collisionManager.LastCollisionPoint +
                                            Vector3.Multiply(collisionManager.LastCollisionNormal, 200);
                ;
                collisionNormalArrow.updateValues();
                collisionNormalArrow.render();

                collisionPoint.Position = collisionManager.LastCollisionPoint;
                collisionPoint.render();
            }

            //Render de mallas
            foreach (var mesh in escenario.Meshes)
            {
                mesh.render();
            }

            //Render personaje
            personaje.animateAndRender(ElapsedTime);
            if (showBB)
            {
                characterSphere.render();
            }

            //Render linea
            directionArrow.render();

            //Render SkyBox
            skyBox.render();

            PostRender();
        }

        public override void Dispose()
        {
            escenario.disposeAll();
            personaje.dispose();
            skyBox.dispose();
            collisionNormalArrow.dispose();
            directionArrow.dispose();
        }
    }
}