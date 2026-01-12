---
name: winforms-ui-flicker-fix
description: Activate for any WinForms startup flickering, ghost images, flashing outlines, or painting issues in Syncfusion-themed apps (Ribbon, DockingManager).
---
<<<<<<< HEAD
=======

>>>>>>> main
Best fixes (Syncfusion-recommended, from https://help.syncfusion.com/windowsforms/common/troubleshooting):

1. Primary: Enable WS_EX_COMPOSITED on MainForm:
   protected override CreateParams CreateParams {
<<<<<<< HEAD
       get {
           var cp = base.CreateParams;
           cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
           return cp;
       }
=======
   get {
   var cp = base.CreateParams;
   cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
   return cp;
   }
>>>>>>> main
   }

2. Double buffering:
   this.DoubleBuffered = true;
   ribbon.DoubleBuffered = true;

3. Batch docking if showing initial panels:
   dockingManager.LockDockPanelsUpdate(true); // or SuspendLayout
   // show panels
   dockingManager.LockDockPanelsUpdate(false);

<<<<<<< HEAD
Prioritize #1 — it resolves 90% of load-time flicker in themed Syncfusion forms.
=======
Prioritize #1 — it resolves 90% of load-time flicker in themed Syncfusion forms.
>>>>>>> main
