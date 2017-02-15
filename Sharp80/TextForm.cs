using System;
using System.Windows.Forms;

namespace Sharp80
{
    public partial class TextForm : Form
    {
        public TextForm()
        {
            InitializeComponent();
        }
        public void ShowText(string Text, string Caption)
        {
            txtText.Text = Text;
            this.Text = Caption;
        }
        public TextBox TextBox
        {
            get { return txtText; }
        }
    }
}