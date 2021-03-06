﻿using System;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using TGC.Core.Direct3D;
using TGC.Core.Example;
using TGC.Core.Input;
using TGC.Core.Shaders;
using TGC.Core.Sound;
using TGC.Core.Textures;
using TGC.Viewer.Properties;
using TGC.Viewer.UI;

namespace TGC.Viewer.Model
{
    public class ViewerModel
    {
        private ViewerForm Form { get; set; }

        /// <summary>
        ///     Obtener o parar el estado del RenderLoop.
        /// </summary>
        public bool ApplicationRunning { get; set; }

        /// <summary>
        ///     Cargador de ejemplos
        /// </summary>
        public ExampleLoader ExampleLoader { get; private set; }

        public void InitGraphics(ViewerForm form, TreeView treeViewExamples, Panel panel3D,
            ToolStripStatusLabel toolStripStatusCurrentExample)
        {
            ApplicationRunning = true;

            Form = form;

            //Configuracion
            var settings = Settings.Default;

            D3DDevice.Instance.InitializeD3DDevice(panel3D);

            //Iniciar otras herramientas
            TgcD3dInput.Instance.Initialize(Form, panel3D);
            TgcDirectSound.Instance.InitializeD3DDevice(Form);

            //Directorio actual de ejecucion
            var currentDirectory = Environment.CurrentDirectory + "\\";

            //Cargar shaders del framework
            TgcShaders.Instance.loadCommonShaders(currentDirectory + settings.ShadersDirectory + settings.CommonShaders);
        }

        public void LoadExamples(TreeView treeViewExamples, FlowLayoutPanel flowLayoutPanelModifiers,
            DataGridView dataGridUserVars)
        {
            //Configuracion
            var settings = Settings.Default;

            //Directorio actual de ejecucion
            var currentDirectory = Environment.CurrentDirectory + "\\";

            //Cargo los ejemplos en el arbol
            ExampleLoader = new ExampleLoader(currentDirectory + settings.MediaDirectory,
                currentDirectory + settings.ShadersDirectory, dataGridUserVars, flowLayoutPanelModifiers);
            ExampleLoader.LoadExamplesInGui(treeViewExamples, currentDirectory);
        }

        public void InitRenderLoop()
        {
            while (ApplicationRunning)
            {
                //Renderizo si es que hay un ejemplo activo
                if (ExampleLoader.CurrentExample != null)
                {
                    //Solo renderizamos si la aplicacion tiene foco, para no consumir recursos innecesarios
                    if (Form.ApplicationActive())
                    {
                        ExampleLoader.CurrentExample.Update();
                        ExampleLoader.CurrentExample.Render();
                    }
                    else
                    {
                        //Si no tenemos el foco, dormir cada tanto para no consumir gran cantidad de CPU
                        Thread.Sleep(100);
                    }
                }
                // Process application messages
                Application.DoEvents();
            }
        }

        public void Wireframe(bool state)
        {
            if (state)
            {
                D3DDevice.Instance.FillModeWireFrame();
            }
            else
            {
                D3DDevice.Instance.FillModeWireSolid();
            }
        }

        public void ContadorFPS(bool state)
        {
            ExampleLoader.CurrentExample.FPS = state;
        }

        public void AxisLines(bool state)
        {
            ExampleLoader.CurrentExample.AxisLines.Enable = state;
        }

        /// <summary>
        ///     Arranca a ejecutar un ejemplo.
        ///     Para el ejemplo anterior, si hay alguno.
        /// </summary>
        /// <param name="example"></param>
        public void ExecuteExample(TgcExample example)
        {
            StopCurrentExample();

            //Ejecutar Init
            try
            {
                example.ResetDefaultConfig();
                example.Init();
                ExampleLoader.CurrentExample = example;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error en Init() de ejemplo: " + example.Name, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        ///     Deja de ejecutar el ejemplo actual
        /// </summary>
        public void StopCurrentExample()
        {
            if (ExampleLoader.CurrentExample != null)
            {
                ExampleLoader.CurrentExample.Dispose();
                ExampleLoader.CurrentExample = null;
            }
        }

        /// <summary>
        ///     Finaliza el render loop y hace dispose del ejemplo y recursos
        /// </summary>
        public void Dispose()
        {
            ApplicationRunning = false;

            if (ExampleLoader.CurrentExample != null)
            {
                ExampleLoader.CurrentExample.Dispose();
            }

            //Liberar Device al finalizar la aplicacion
            D3DDevice.Instance.Dispose();
            TexturesPool.Instance.clearAll();
        }

        /// <summary>
        ///     Cuando el Direct3D Device se resetea.
        ///     Se reinica el ejemplo actual, si hay alguno.
        /// </summary>
        public void OnResetDevice()
        {
            var exampleBackup = ExampleLoader.CurrentExample;

            if (exampleBackup != null)
            {
                StopCurrentExample();
            }

            D3DDevice.Instance.DoResetDevice();

            if (exampleBackup != null)
            {
                ExecuteExample(exampleBackup);
            }
        }

        public void DownloadMediaFolder()
        {
            var client = new WebClient();

            client.DownloadProgressChanged += client_DownloadProgressChanged;
            client.DownloadFileCompleted += client_DownloadFileCompleted;

            // Starts the download
            //client.DownloadFileAsync(new Uri("http://tgcutn.com.ar/images/logotp.png"), @"C:\Users\Mito\Downloads\logotp.png");

            //btnStartDownload.Text = "Download In Process";
            //btnStartDownload.Enabled = false;
        }

        private void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            var bytesIn = double.Parse(e.BytesReceived.ToString());
            var totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            var percentage = bytesIn / totalBytes * 100;

            Console.Write(int.Parse(Math.Truncate(percentage).ToString()));
        }

        private void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            MessageBox.Show("Download Completed");

            //btnStartDownload.Text = "Start Download";
            //btnStartDownload.Enabled = true;
        }
    }
}