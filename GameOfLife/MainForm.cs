using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameOfLife;

public partial class MainForm : Form
{
    private const int CellSize = 10;
    private const int WidthCells = 70;
    private const int HeightCells = 50;

    private readonly Func<int, bool, bool> _lifeRules = (p, state) => p switch
    {
        3 => true,
        2 => state,
        _ => false
    };

    private readonly bool[,] _table = new bool[HeightCells, WidthCells];
    private readonly bool[,] _tableNew = new bool[HeightCells, WidthCells];
    private readonly Random _random = new();

    private CancellationTokenSource? _cancellation;
    private Task? _runningTask;
    private int _generation;

    private Panel _board = null!;
    private Button _sequentialButton = null!;
    private Button _parallelButton = null!;
    private Button _cancelButton = null!;
    private NumericUpDown _delayInput = null!;
    private Label _generationLabel = null!;

    public MainForm()
    {
        InitializeComponent();
        InitTable();
    }

    private void InitializeComponent()
    {
        Text = "Игра ЖИЗНЬ Дж. Конвея";
        ClientSize = new Size(WidthCells * CellSize + 20, HeightCells * CellSize + 80);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        _board = new Panel
        {
            Location = new Point(10, 10),
            Size = new Size(WidthCells * CellSize, HeightCells * CellSize),
            BorderStyle = BorderStyle.FixedSingle
        };
        _board.Paint += (_, e) => DrawBoard(e.Graphics);
        Controls.Add(_board);

        _sequentialButton = new Button
        {
            Text = "Последовательно",
            Location = new Point(10, _board.Bottom + 10),
            Width = 120
        };
        _sequentialButton.Click += (_, _) => StartSimulation(false);
        Controls.Add(_sequentialButton);

        _parallelButton = new Button
        {
            Text = "Параллельно",
            Location = new Point(_sequentialButton.Right + 10, _board.Bottom + 10),
            Width = 120
        };
        _parallelButton.Click += (_, _) => StartSimulation(true);
        Controls.Add(_parallelButton);

        _cancelButton = new Button
        {
            Text = "Отмена",
            Location = new Point(_parallelButton.Right + 10, _board.Bottom + 10),
            Width = 100,
            Enabled = false
        };
        _cancelButton.Click += (_, _) => StopSimulation();
        Controls.Add(_cancelButton);

        _delayInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 200,
            Value = 50,
            Increment = 10,
            Location = new Point(_cancelButton.Right + 10, _board.Bottom + 12),
            Width = 80
        };
        Controls.Add(_delayInput);

        _generationLabel = new Label
        {
            Text = "Поколение: 0",
            Location = new Point(_delayInput.Right + 10, _board.Bottom + 15),
            AutoSize = true
        };
        Controls.Add(_generationLabel);
    }

    private void InitTable()
    {
        for (int i = 0; i < HeightCells; i++)
        {
            for (int j = 0; j < WidthCells; j++)
            {
                _table[i, j] = _random.NextDouble() > 0.7;
            }
        }
    }

    private void DrawBoard(Graphics g)
    {
        g.Clear(Color.White);
        using var aliveBrush = new SolidBrush(Color.IndianRed);
        using var gridPen = new Pen(Color.LightGray);

        for (int i = 0; i < HeightCells; i++)
        {
            for (int j = 0; j < WidthCells; j++)
            {
                var rect = new Rectangle(j * CellSize, i * CellSize, CellSize, CellSize);
                if (_table[i, j])
                {
                    g.FillRectangle(aliveBrush, rect);
                }
                g.DrawRectangle(gridPen, rect);
            }
        }
    }

    private void StartSimulation(bool parallel)
    {
        StopSimulation();
        InitTable();
        _generation = 0;
        _generationLabel.Text = "Поколение: 0";
        _cancellation = new CancellationTokenSource();
        _cancelButton.Enabled = true;
        _sequentialButton.Enabled = false;
        _parallelButton.Enabled = false;

        _runningTask = Task.Run(async () =>
        {
            try
            {
                var token = _cancellation!.Token;
                while (!token.IsCancellationRequested)
                {
                    if (parallel)
                    {
                        StepParallel(token);
                    }
                    else
                    {
                        StepSequential();
                    }

                    SwapTables();
                    _generation++;
                    UpdateUiSafe();
                    await Task.Delay(TimeSpan.FromMilliseconds((double)_delayInput.Value), token);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            finally
            {
                Invoke(new Action(StopSimulation));
            }
        });
    }

    private void StopSimulation()
    {
        _cancellation?.Cancel();
        _runningTask = null;
        _cancellation = null;
        _cancelButton.Enabled = false;
        _sequentialButton.Enabled = true;
        _parallelButton.Enabled = true;
    }

    private void UpdateUiSafe()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateUiSafe));
            return;
        }

        _generationLabel.Text = $"Поколение: {_generation}";
        _board.Invalidate();
    }

    private void StepSequential()
    {
        for (int i = 0; i < HeightCells; i++)
        {
            for (int j = 0; j < WidthCells; j++)
            {
                int p = CalcPotential(_table, i, j);
                _tableNew[i, j] = _lifeRules(p, _table[i, j]);
            }
        }
    }

    private void StepParallel(CancellationToken token)
    {
        Parallel.For(0, HeightCells, new ParallelOptions { CancellationToken = token }, i =>
        {
            for (int j = 0; j < WidthCells; j++)
            {
                int p = CalcPotential(_table, i, j);
                _tableNew[i, j] = _lifeRules(p, _table[i, j]);
            }
        });
    }

    private static int CalcPotential(bool[,] table, int i, int j)
    {
        int p = 0;
        for (int x = i - 1; x <= i + 1; x++)
        {
            for (int y = j - 1; y <= j + 1; y++)
            {
                if (x < 0 || x >= HeightCells || y < 0 || y >= WidthCells || (x == i && y == j))
                {
                    continue;
                }

                if (table[x, y])
                {
                    p++;
                }
            }
        }

        return p;
    }

    private void SwapTables()
    {
        for (int i = 0; i < HeightCells; i++)
        {
            for (int j = 0; j < WidthCells; j++)
            {
                _table[i, j] = _tableNew[i, j];
            }
        }
    }
}
