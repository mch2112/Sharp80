namespace Sharp80
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        /// 
        private bool isDisposing = false;
        protected override void Dispose(bool disposing)
        {
            if (!isDisposing && !IsDisposed)
            {
                isDisposing = true;
                if (disposing && (components != null))
                {
                    components.Dispose();
                }

                base.Dispose(disposing);

                if (!screen.IsDisposed)
                    screen.Dispose();
                if (!uic.IsDisposed)
                    uic.Dispose();
                if (!keyboard.IsDisposed)
                    keyboard.Dispose();
                
                isDisposing = false;
            }
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(814, 359);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(6);
            this.Name = "MainForm";
            this.Activated += new System.EventHandler(this.Form_Activated);
            this.Deactivate += new System.EventHandler(this.Form_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_FormClosing);
            this.Load += new System.EventHandler(this.Form_Load);
            this.ResumeLayout(false);

        }

        #endregion
    }
}