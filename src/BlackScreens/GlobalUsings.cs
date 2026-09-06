// The folders under src exist to keep files findable, not to hold the app apart from itself: it is
// one small assembly and nearly every file reaches across two or three of them. Declaring the
// namespaces once here keeps that from turning into the same block of usings at the top of forty
// files. Ui and Themes are deliberately absent, so that the parts of the app that have nothing to do
// with the settings window still have to ask for it by name.
global using BlackScreens.App;
global using BlackScreens.Configuration;
global using BlackScreens.Detection;
global using BlackScreens.Interop;
global using BlackScreens.Monitors;
global using BlackScreens.Overlays;
global using BlackScreens.Updates;
