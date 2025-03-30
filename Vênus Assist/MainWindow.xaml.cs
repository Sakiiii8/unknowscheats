using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Color = System.Drawing.Color;

namespace ColorAimbot
{
    public partial class MainWindow : Window
    {
        private bool aimbotAtivo = false;
        private Thread? aimbotThread;
        private readonly Random random = new();
        private readonly List<Ellipse> particles = new();
        private readonly DispatcherTimer particleTimer;

        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        const int MOUSEEVENTF_MOVE = 0x0001;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                CriarParticulas();
                particleTimer.Start();
                IniciarToggleComTeclaInsert(); // <- Chamando o controle de Insert aqui
            };

            particleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            particleTimer.Tick += (s, e) => AtualizarParticulas();
        }

        private void CriarParticulas()
        {
            for (int i = 0; i < 30; i++)
            {
                Ellipse p = new()
                {
                    Width = 4,
                    Height = 4,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 180, 120, 255)), // roxo mais visível
                    Opacity = 0.9
                };
                Canvas.SetLeft(p, random.NextDouble() * Width);
                Canvas.SetTop(p, random.NextDouble() * Height);
                particles.Add(p);
                ParticleCanvas.Children.Add(p);
            }
        }

        private void AtualizarParticulas()
        {
            foreach (var p in particles)
            {
                double top = Canvas.GetTop(p);
                top += 1.2 + random.NextDouble();
                if (top > Height)
                {
                    top = 0;
                    Canvas.SetLeft(p, random.NextDouble() * Width);
                }
                Canvas.SetTop(p, top);
            }
        }

        private void StartAimbot_Click(object sender, RoutedEventArgs e)
        {
            if (aimbotAtivo)
            {
                aimbotAtivo = false;
                aimbotThread = null;
                Dispatcher.BeginInvoke(() => LogText.Text = "Aimbot OFF");
                return;
            }

            aimbotAtivo = true;
            Dispatcher.BeginInvoke(() => LogText.Text = "Aimbot OFF");
            aimbotThread = new Thread(RodarAimbot);
            aimbotThread.SetApartmentState(ApartmentState.STA);
            aimbotThread.IsBackground = true;
            aimbotThread.Start();
        }

        private void RodarAimbot()
        {
            try
            {
                Dispatcher.BeginInvoke(() => LogText.Text = "Aimbot ON");

                string hexColor = string.Empty;
                int tolerancia = 50;
                int fov = 120;
                bool suavizar = false;

                Dispatcher.Invoke(() =>
                {
                    hexColor = ColorBox.Text.Replace("#", "");
                    fov = int.Parse(FovBox.Text);
                    suavizar = SmoothCheck.IsChecked == true;
                });

                int r = int.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

                int screenW = (int)SystemParameters.PrimaryScreenWidth;
                int screenH = (int)SystemParameters.PrimaryScreenHeight;
                int cx = screenW / 2;
                int cy = screenH / 2;

                while (aimbotAtivo)
                {
                    // Ativado com botão esquerdo (0x01) OU direito (0x02)
                    if (GetAsyncKeyState(0x02) < 0 || GetAsyncKeyState(0x01) < 0)
                    {
                        using (Bitmap bmp = new(fov, fov))
                        {
                            using (Graphics gBmp = Graphics.FromImage(bmp))
                            {
                                gBmp.CopyFromScreen(cx - fov / 2, cy - fov / 2, 0, 0, new System.Drawing.Size(fov, fov));

                                int melhorX = -1, melhorY = -1;
                                double menorDistancia = double.MaxValue;

                                for (int x = 0; x < fov; x++)
                                {
                                    for (int y = 0; y < fov; y++)
                                    {
                                        Color pixel = bmp.GetPixel(x, y);
                                        if (Math.Abs(pixel.R - r) <= tolerancia &&
                                            Math.Abs(pixel.G - g) <= tolerancia &&
                                            Math.Abs(pixel.B - b) <= tolerancia)
                                        {
                                            double distancia = Math.Sqrt(Math.Pow(x - fov / 2, 2) + Math.Pow(y - fov / 2, 2));
                                            if (distancia < menorDistancia)
                                            {
                                                menorDistancia = distancia;
                                                melhorX = x;
                                                melhorY = y;
                                            }
                                        }
                                    }
                                }

                                if (melhorX != -1)
                                {
                                    int realX = cx - fov / 2 + melhorX;
                                    int realY = cy - fov / 2 + melhorY;

                                    int dx = realX - cx;
                                    int dy = realY - cy;

                                    double fator = suavizar ? 0.35 : 0.15;
                                    dx = (int)(dx * fator);
                                    dy = (int)(dy * fator);

                                    mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, 0);

                                    Dispatcher.BeginInvoke(() =>
                                    {
                                        LogText.Text = $"Mira: {realX},{realY} (Δx={dx}, Δy={dy})";
                                    });
                                }
                            }
                        }
                    }

                    Thread.Sleep(0);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    LogText.Text = "Erro: " + ex.Message;
                });
            }
        }

        private void Minimizar_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Fechar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ============================
        //      TECLA INSERT
        // ============================
        private readonly DispatcherTimer toggleTimer = new DispatcherTimer();
        private bool podeAlternar = true;

        private void IniciarToggleComTeclaInsert()
        {
            toggleTimer.Interval = TimeSpan.FromMilliseconds(100);
            toggleTimer.Tick += (s, e) =>
            {
                if (GetAsyncKeyState(0x2D) < 0) // 0x2D = Insert
                {
                    if (podeAlternar)
                    {
                        this.Visibility = this.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                        podeAlternar = false;
                    }
                }
                else
                {
                    podeAlternar = true;
                }
            };
            toggleTimer.Start();
        }
    }
}
