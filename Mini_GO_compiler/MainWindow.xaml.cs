using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using WinForm = System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Mini_GO_compiler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string currentDirectory;
        private string currentFile;
        
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSyntaxHighlighting();
        }

        private void LoadSyntaxHighlighting()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Mini_GO_compiler.ide.syntaxHighlighter.xml";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (XmlReader reader = XmlReader.Create(stream))
            {
                var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance); 
                HighlightingManager.Instance.RegisterHighlighting("Mini Go", new[] { ".go" }, definition);
            }
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mini Go");
        }

        private void OpenFolder_click(object sender, RoutedEventArgs e)
        {
            WinForm.FolderBrowserDialog dialog = new WinForm.FolderBrowserDialog();
            WinForm.DialogResult result = dialog.ShowDialog();

            if (result == WinForm.DialogResult.OK)
            {
                currentDirectory = dialog.SelectedPath;
                ArchivosView.Children.Clear();
                TextBlock mainDirectory = new TextBlock();
                mainDirectory.Text = currentDirectory;
                mainDirectory.TextAlignment = TextAlignment.Center;
                mainDirectory.Foreground = new SolidColorBrush(Colors.White);
                ArchivosView.Children.Add(mainDirectory);
                ShowDirectories(sender, e, ArchivosView, currentDirectory);
            }
        }

        private void ShowDirectories(object sender, RoutedEventArgs e, StackPanel actualPanel, string actualDirectory)
        {
            string[] dirs = Directory.GetDirectories(actualDirectory);
            foreach (string dir in dirs)
            {
                StackPanel panel = new StackPanel();
                Button dirButton = new Button();
                string[] dirParts = dir.Split("\\"); 
                dirButton.Content = "📁> "+dirParts[dirParts.Length - 1];
                string dirPath = dir;
                panel.Children.Add(dirButton);
                var abierto = false;
                dirButton.Click += (s, args) =>
                {
                    if (!abierto)
                    {
                        dirButton.Content = "📁\\ "+dirParts[dirParts.Length - 1];
                        ShowDirectories(s, args, panel, dirPath);  
                        abierto = true;
                    }
                    else
                    {
                        panel.Children.Clear();
                        dirButton.Content = "📁> "+dirParts[dirParts.Length - 1];
                        panel.Children.Add(dirButton);
                        abierto = false;
                    }
                };
                actualPanel.Children.Add(panel);
            }
            string[] files = Directory.GetFiles(actualDirectory);
            foreach (string file in files)
            {
                Button fileButton = new Button();
                string[] fileParts = file.Split("\\");
                fileButton.Content = "📄"+fileParts[fileParts.Length - 1];
                string context = File.ReadAllText(file);
                //Console.WriteLine(context);
                fileButton.Click += (s, args) =>
                {
                    currentFile = file;
                    TextBlock1.Visibility = Visibility.Collapsed;
                    editor.Visibility = Visibility.Visible;
                    editor.Text = context;
                };
                actualPanel.Children.Add(fileButton);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (currentFile != null)
            {
                string newContent = editor.Text;
                File.WriteAllText(currentFile, newContent);
                ArchivosView.Children.Clear();
                TextBlock mainDirectory = new TextBlock();
                mainDirectory.Text = currentDirectory;
                mainDirectory.TextAlignment = TextAlignment.Center;
                mainDirectory.Foreground = new SolidColorBrush(Colors.White);
                ArchivosView.Children.Add(mainDirectory);
                ShowDirectories(sender, e, ArchivosView, currentDirectory);
                MessageBox.Show("Saved successfully!");
            }
            else
            {
                TextBlock1.Text = "No file has been selected";
            }
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Mini Go Compiler v1.0");
        }

        private void Button_Hello_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello World!");
            //TextBlock1.Text = "Hello World2!";
        }
    }
}