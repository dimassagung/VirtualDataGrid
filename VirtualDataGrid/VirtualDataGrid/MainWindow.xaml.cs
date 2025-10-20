using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VirtualDataGrid.Core;
using System.Collections.ObjectModel;
using VirtualDataGrid.Controls;
using System.Reflection.PortableExecutable;

namespace VirtualGridDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        void Test()
        {
            Generate(100);
        }


        public ObservableCollection<DemoEntity> Items { get; } = new();
        private void Generate(int count)
        {
            var rnd = new Random(123);
            string[] names = { "Alice", "Bob", "Charlie", "David", "Eve", "Frank", "Grace", "Hana", "Iwan", "Joko", "Kiki" };
            string[] cats = { "A", "B", "C", "D" };
            for (int i = 0; i < count; i++)
            {
                var e = new DemoEntity
                {
                    Id = i + 1,
                    Name = names[rnd.Next(names.Length)] + " " + rnd.Next(1000, 9999),
                    Category = cats[rnd.Next(cats.Length)],
                    Amount = Math.Round(rnd.NextDouble() * 100000, 2),
                    Created = DateTime.UtcNow.AddDays(-rnd.Next(0, 1000))
                };
                // add on UI thread
                Items.Add(e);
            }
            ColumnCollection columns = new ColumnCollection();
            columns.Add(new VirtualDataGridColumn("hId", "Id"));
            columns.Add(new VirtualDataGridColumn("hName", "Name"));
            columns.Add(new VirtualDataGridColumn("hCategory", "Category"));

            VirtualDataGrid.Controls.VirtualDataGrid _grid = new VirtualDataGrid.Controls.VirtualDataGrid();
            _grid.ItemsSource = Items;
            _grid.Columns = columns;
        }
        public class DemoEntity
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public double Amount { get; set; }
            public DateTime Created { get; set; }
            public override string ToString() => $"{Id} {Name} {Category} {Amount} {Created:yyyy-MM-dd}";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Test();
        }
    }


}