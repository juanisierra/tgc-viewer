using Microsoft.DirectX;
using Microsoft.DirectX.DirectInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using TGC.Core.Input;

namespace TGC.Examples.Engine2D
{
    internal class Spaceship : GameObject
    {
        //El angulo al que apunta la nave.
        private float angle;

        //El angulo al mouse.
        private float angleToMousePointer;

        //La posicion del centro del sprite
        private Vector2 centerPosition;

        private int contadorAnimacion;

        //El numero de sprite dentro del spritesheet
        private int currentSprite;

        private float elapsedTime;

        //Cuanto tiempo paso desde que se disparo el ultimo misil.
        private float misilRateCounter;

        //La posicion de la nave.
        public Vector2 Position;

        //El factor de escalado.
        private float size;

        //El bitmap del spritesheet.
        private Bitmap spaceshipBitmap;

        //La velocidad de la nave.
        private Vector2 speed;

        //Los distintos sprites de animacion.
        private List<Sprite> sprites;

        public Vector2 spriteSize;

        //El estado de la nave.
        private StateEnum state;

        public void Load(Bitmap bitmap)
        {
            spaceshipBitmap = bitmap;

            sprites = new List<Sprite>();

            spriteSize = new Vector2(41, 44);
            size = 2.0f;

            Sprite newSprite;
            for (var i = 0; i < 3; i++)
            {
                newSprite = new Sprite();
                newSprite.Bitmap = spaceshipBitmap;
                newSprite.SrcRect = new Rectangle(i * (int)spriteSize.X, 0, (int)spriteSize.X, (int)spriteSize.Y);
                newSprite.Scaling = new Vector2(size, size);
                sprites.Add(newSprite);
            }

            currentSprite = 0;
            state = StateEnum.Idle;

            Position = new Vector2(100, 100);
            speed = new Vector2(0, 0);

            angleToMousePointer = 0;

            RestartPosition();

            GameManager.Instance.userVars.addVar("elapsed");
            GameManager.Instance.userVars.addVar("speedX");
            GameManager.Instance.userVars.addVar("speedY");

            GameManager.Instance.userVars.addVar("PosX");
            GameManager.Instance.userVars.addVar("PosY");

            GameManager.Instance.userVars.addVar("MousePosX");
            GameManager.Instance.userVars.addVar("MousePosY");

            GameManager.Instance.userVars.addVar("AngleMouse");

            GameManager.Instance.userVars.addVar("Misiles");
        }

        private void fireMissile()
        {
            //Si paso el tiempo suficiente
            if (misilRateCounter > 0.10f)
            {
                GameManager.Instance.fireMissile(centerPosition, angle - (float)Math.PI / 2.0f);
                misilRateCounter = 0;
            }
        }

        public void RestartPosition()
        {
            Position.X = GameManager.ScreenWidth / 2;
            Position.Y = GameManager.ScreenHeight / 2;
        }

        public override void Update(float elapsedTime)
        {
            this.elapsedTime = elapsedTime;

            //float dirX = 0;
            float dirY = 0;

            state = StateEnum.Idle;
            if (TgcD3dInput.Instance.keyDown(Key.A))
            {
                //  dirX = -1;
                // state = StateEnum.Moving;
            }
            if (TgcD3dInput.Instance.keyDown(Key.D))
            {
                // dirX = 1;
                // state = StateEnum.Moving;
            }
            if (TgcD3dInput.Instance.keyDown(Key.W))
            {
                dirY = -1;
                state = StateEnum.Moving;
            }
            if (TgcD3dInput.Instance.keyDown(Key.S))
            {
                dirY = 1;
                state = StateEnum.Moving;
            }

            if (TgcD3dInput.Instance.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                fireMissile();
            }

            if (state == StateEnum.Idle)
            {
                currentSprite = 0;
            }
            else
            {
                currentSprite = contadorAnimacion;

                if (contadorAnimacion > 1)
                {
                    contadorAnimacion = 1;
                }
                else
                {
                    contadorAnimacion++;
                }
            }

            //Las constantes
            const float maxSpeed = 400.0f;
            const float acceleration = 300.0f;
            const float deacceleration = 300.0f;
            //const float Epsilon = 0.2f;

            var spriteMouseVector = new Vector2();
            var mouseVector = new Vector2(TgcD3dInput.Instance.Xpos, TgcD3dInput.Instance.Ypos);
            spriteMouseVector = Vector2.Subtract(mouseVector,
                Position + new Vector2(spriteSize.X / 2 * size, spriteSize.Y / 2 * size));

            if (spriteMouseVector.Length() > 10f)
                angleToMousePointer = (float)Math.Atan2(spriteMouseVector.Y, spriteMouseVector.X);

            var MouseXAngle = (float)Math.Cos(angleToMousePointer);
            var MouseYAngle = (float)Math.Sin(angleToMousePointer);

            angle = angleToMousePointer + (float)Math.PI / 2.0f;

            if (dirY == 0)
            {
                speed.X -= Math.Sign(speed.X) * deacceleration * elapsedTime;
                speed.Y -= Math.Sign(speed.Y) * deacceleration * elapsedTime;
            }

            if (dirY == -1)
            {
                speed.X += acceleration * MouseXAngle * elapsedTime;
                speed.Y += acceleration * MouseYAngle * elapsedTime;
            }

            //Limitar la velocidad
            if (speed.Length() > maxSpeed)
            {
                speed.X = maxSpeed * MouseXAngle;
                speed.Y = maxSpeed * MouseYAngle;
            }

            Position.X += speed.X * elapsedTime;
            Position.Y += speed.Y * elapsedTime;

            centerPosition = Position + spriteSize * 0.5f * size;

            sprites[currentSprite].Position = Position;
            sprites[currentSprite].Rotation = angle;

            sprites[currentSprite].RotationCenter = new Vector2(spriteSize.X / 2 * size, spriteSize.Y / 2 * size);

            misilRateCounter += elapsedTime;

            if (Position.X > GameManager.ScreenWidth + spriteSize.X)
                Position.X = -spriteSize.X;

            if (Position.X < -spriteSize.X)
                Position.X = GameManager.ScreenWidth + spriteSize.X;

            if (Position.Y > GameManager.ScreenHeight + spriteSize.Y)
                Position.Y = -spriteSize.Y;

            if (Position.Y < -spriteSize.Y)
                Position.Y = GameManager.ScreenHeight + spriteSize.Y;

            GameManager.Instance.userVars.setValue("elapsed", elapsedTime);
            GameManager.Instance.userVars.setValue("speedX", speed.X);
            GameManager.Instance.userVars.setValue("speedY", speed.Y);

            GameManager.Instance.userVars.setValue("PosX", Position.X);
            GameManager.Instance.userVars.setValue("PosY", Position.Y);
            GameManager.Instance.userVars.setValue("MousePosX", TgcD3dInput.Instance.Xpos);
            GameManager.Instance.userVars.setValue("MousePosY", TgcD3dInput.Instance.Ypos);
            GameManager.Instance.userVars.setValue("AngleMouse", angleToMousePointer * 360 / (2 * Math.PI));
        }

        public override void Render(float elapsedTime, Drawer drawer)
        {
            drawer.DrawSprite(sprites[currentSprite]);
        }

        private enum StateEnum
        {
            Idle,
            Moving
        }
    }
}