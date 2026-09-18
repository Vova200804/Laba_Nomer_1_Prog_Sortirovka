namespace LABA2;

partial class Form1
{
  private System.ComponentModel.IContainer components = null!;

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      components?.Dispose();
    }

    base.Dispose(disposing);
  }

  private void InitializeComponent()
  {
    components = new System.ComponentModel.Container();
    SuspendLayout();
    AutoScaleDimensions = new SizeF(7F, 15F);
    AutoScaleMode = AutoScaleMode.Font;
    ClientSize = new Size(800, 450);
    Name = "Form1";
    Text = "Лабораторная работа №4";
    ResumeLayout(false);
  }
}
