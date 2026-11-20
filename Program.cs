using System;
using SAPbouiCOM;
using WinForms = System.Windows.Forms;

namespace AddOnAutoLotes
{
    static class Program
    {
        private static SAPbouiCOM.Application _sboApp;
        private static BatchAutoAssigner _assigner;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // 1. Establecer conexión con SAP Business One Client
                SetApplication(args);

                if (_sboApp == null)
                {
                    WinForms.MessageBox.Show("No se pudo conectar a una sesión activa de SAP Business One.", 
                                             "AddOnAutoLotes", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    return;
                }

                // 2. Instanciar la lógica de autoasignación
                _assigner = new BatchAutoAssigner(_sboApp);

                // 3. Configurar filtros de eventos para alto rendimiento
                SetEventFilters();

                // 4. Suscribir a los eventos
                _sboApp.ItemEvent += SBO_Application_ItemEvent;
                _sboApp.AppEvent += SBO_Application_AppEvent;

                // 5. Escanear formularios ya abiertos actualmente en SAP (por si la ventana ya estaba abierta)
                ScanExistingForms();

                _sboApp.SetStatusBarMessage("Add-on Autoasignación de Lotes conectado.", 
                                            BoMessageTime.bmt_Short, false);

                // 6. Iniciar el bucle de mensajes
                WinForms.Application.Run();
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show("Error fatal en AddOnAutoLotes: " + ex.Message, 
                                         "AddOnAutoLotes", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private static void SetApplication(string[] args)
        {
            SboGuiApi sboGuiApi = new SboGuiApi();
            string connStr = null;

            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                connStr = args[0];
            }
            else
            {
                connStr = "0030002C0030002C00530041005000420044005F00440061007400650076002C0050004C006F006D0056004900490056";
            }

            sboGuiApi.Connect(connStr);
            _sboApp = sboGuiApi.GetApplication();
        }

        private static void SetEventFilters()
        {
            try
            {
                EventFilters oFilters = new EventFilters();

                EventFilter filterLoad = oFilters.Add(BoEventTypes.et_FORM_LOAD);
                filterLoad.AddEx("42");
                filterLoad.AddEx("41");

                EventFilter filterActivate = oFilters.Add(BoEventTypes.et_FORM_ACTIVATE);
                filterActivate.AddEx("42");
                filterActivate.AddEx("41");

                EventFilter filterVisible = oFilters.Add(BoEventTypes.et_FORM_VISIBLE);
                filterVisible.AddEx("42");
                filterVisible.AddEx("41");

                EventFilter filterResize = oFilters.Add(BoEventTypes.et_FORM_RESIZE);
                filterResize.AddEx("42");
                filterResize.AddEx("41");

                EventFilter filterClick = oFilters.Add(BoEventTypes.et_ITEM_PRESSED);
                filterClick.AddEx("42");
                filterClick.AddEx("41");

                _sboApp.SetFilter(oFilters);
            }
            catch (Exception ex)
            {
                Console.WriteLine("No se pudieron establecer los filtros de eventos: " + ex.Message);
            }
        }

        private static void ScanExistingForms()
        {
            try
            {
                for (int i = 0; i < _sboApp.Forms.Count; i++)
                {
                    try
                    {
                        Form f = _sboApp.Forms.Item(i);
                        if (_assigner.IsTargetForm(f.TypeEx))
                        {
                            _assigner.AddButtonToForm(f);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            try
            {
                if (!_assigner.IsTargetForm(pVal.FormTypeEx))
                    return;

                if (!pVal.BeforeAction && 
                    (pVal.EventType == BoEventTypes.et_FORM_LOAD || 
                     pVal.EventType == BoEventTypes.et_FORM_VISIBLE || 
                     pVal.EventType == BoEventTypes.et_FORM_ACTIVATE))
                {
                    Form oForm = _sboApp.Forms.Item(FormUID);
                    _assigner.AddButtonToForm(oForm);
                }

                if (!pVal.BeforeAction && pVal.EventType == BoEventTypes.et_FORM_RESIZE)
                {
                    _assigner.RepositionButton(_sboApp.Forms.Item(FormUID));
                }

                if (!pVal.BeforeAction && 
                    pVal.EventType == BoEventTypes.et_ITEM_PRESSED && 
                    pVal.ItemUID == BatchAutoAssigner.ButtonUid)
                {
                    Form oForm = _sboApp.Forms.Item(FormUID);
                    _assigner.ExecuteAutoAssignAll(oForm);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en evento ItemEvent: " + ex.Message);
            }
        }

        private static void SBO_Application_AppEvent(BoAppEventTypes EventType)
        {
            switch (EventType)
            {
                case BoAppEventTypes.aet_ShutDown:
                case BoAppEventTypes.aet_CompanyChanged:
                case BoAppEventTypes.aet_ServerTerminition:
                    WinForms.Application.Exit();
                    break;
            }
        }
    }
}
