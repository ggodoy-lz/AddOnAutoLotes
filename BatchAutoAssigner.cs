using System;
using System.Configuration;
using System.Threading;
using System.Windows.Forms;
using SAPbouiCOM;

namespace AddOnAutoLotes
{
    public class BatchAutoAssigner
    {
        private readonly SAPbouiCOM.Application _sboApp;
        private readonly string _buttonCaption;
        private readonly int _buttonWidth;
        private readonly bool _autoConfirm;
        private readonly bool _enableSerials;
        private bool _running;

        public const string ButtonUid = "btnAutoAll";

        public BatchAutoAssigner(SAPbouiCOM.Application sboApp)
        {
            _sboApp = sboApp;

            _buttonCaption = ConfigurationManager.AppSettings["ButtonCaption"] ?? "Autoasignar Todo";

            int width;
            if (!int.TryParse(ConfigurationManager.AppSettings["ButtonWidth"], out width))
                width = 140;
            _buttonWidth = width;

            // Por defecto activamos auto-confirmaciÃ³n para que guarde con OK automÃ¡ticamente
            bool autoConf;
            if (!bool.TryParse(ConfigurationManager.AppSettings["AutoConfirm"], out autoConf))
                autoConf = true;
            _autoConfirm = autoConf;

            bool enableSer;
            if (!bool.TryParse(ConfigurationManager.AppSettings["EnableSerials"], out enableSer))
                enableSer = true;
            _enableSerials = enableSer;
        }

        public bool IsTargetForm(string formTypeEx)
        {
            if (formTypeEx == "42") return true;
            if (_enableSerials && formTypeEx == "41") return true;
            return false;
        }

        public void AddButtonToForm(SAPbouiCOM.Form oForm)
        {
            try
            {
                Item oBtnItem = null;
                try { oBtnItem = oForm.Items.Item(ButtonUid); } catch { }

                if (oBtnItem == null)
                {
                    oBtnItem = oForm.Items.Add(ButtonUid, BoFormItemTypes.it_BUTTON);
                    oBtnItem.Width = _buttonWidth;
                    ((SAPbouiCOM.Button)oBtnItem.Specific).Caption = _buttonCaption;
                }

                PositionButton(oForm, oBtnItem);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al agregar botÃ³n Autoasignar: " + ex.Message);
            }
        }

        // SAP reubica sus Ã­tems nativos al redimensionar, pero no los agregados por add-on.
        // Anclamos siempre a la derecha de 'Cancelar' (Item 2), que SAP mantiene abajo a la izquierda.
        public void RepositionButton(SAPbouiCOM.Form oForm)
        {
            try
            {
                PositionButton(oForm, oForm.Items.Item(ButtonUid));
            }
            catch { }
        }

        private void PositionButton(SAPbouiCOM.Form oForm, Item oBtnItem)
        {
            Item itmCancel = oForm.Items.Item("2");
            oBtnItem.Left = itmCancel.Left + itmCancel.Width + 5;
            oBtnItem.Top = itmCancel.Top;
            oBtnItem.Height = itmCancel.Height;
        }

        private static bool IsAutoCaption(Item itm)
        {
            if (itm.Type != BoFormItemTypes.it_BUTTON) return false;
            try
            {
                string cap = (((SAPbouiCOM.Button)itm.Specific).Caption ?? "").Replace("&", "").ToLower();
                return cap.Contains("auto");
            }
            catch { return false; }
        }

        // Busca el botÃ³n nativo de SelecciÃ³n automÃ¡tica por caption (no confiar en un UID fijo).
        private static Item FindNativeAutoButton(SAPbouiCOM.Form oForm)
        {
            for (int k = 0; k < oForm.Items.Count; k++)
            {
                Item itm = oForm.Items.Item(k);
                if (itm.UniqueID == ButtonUid || itm.UniqueID == "1" || itm.UniqueID == "2") continue;
                if (IsAutoCaption(itm)) return itm;
            }
            return null;
        }

        public void ExecuteAutoAssignAll(SAPbouiCOM.Form oForm)
        {
            // Evita ejecuciones superpuestas: DoEvents dentro del bucle procesa clicks repetidos
            if (_running) return;
            _running = true;
            try { RunAutoAssignAll(oForm); }
            finally { _running = false; }
        }

        private void RunAutoAssignAll(SAPbouiCOM.Form oForm)
        {
            try
            {
                // Matriz 3: LÃ­neas de documentos
                Matrix oMatrix = null;
                try
                {
                    oMatrix = (Matrix)oForm.Items.Item("3").Specific;
                }
                catch
                {
                    _sboApp.StatusBar.SetText("No se encontrÃ³ la matriz de artÃ­culos del documento (Item 3).", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
                    return;
                }

                int totalRows = oMatrix.RowCount;
                if (totalRows == 0)
                {
                    _sboApp.StatusBar.SetText("No hay artÃ­culos en la lista para asignar.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
                    return;
                }

                // Localizar el botÃ³n de AutoselecciÃ³n nativo
                Item oItemAuto = FindNativeAutoButton(oForm);

                if (oItemAuto == null)
                {
                    _sboApp.StatusBar.SetText("No se encontrÃ³ el botÃ³n nativo de AutoselecciÃ³n.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
                    return;
                }

                _sboApp.SetStatusBarMessage("Iniciando autoasignaciÃ³n masiva de lotes...", BoMessageTime.bmt_Short, false);

                int processed = 0;

                // Recorrer cada fila de la matriz
                for (int row = 1; row <= totalRows; row++)
                {
                    try
                    {
                        // 1. Seleccionar y hacer clic en la fila para que SAP active ese Ã­tem y cargue sus lotes disponibles
                        try { oMatrix.SelectRow(row, true, false); } catch { }
                        try { oMatrix.Columns.Item(0).Cells.Item(row).Click(BoCellClickType.ct_Regular); } catch { }
                        try { oMatrix.Columns.Item(1).Cells.Item(row).Click(BoCellClickType.ct_Regular); } catch { }

                        // Dar tiempo a la interfaz de SAP para sincronizar los lotes de la lÃ­nea
                        System.Windows.Forms.Application.DoEvents();
                        Thread.Sleep(60);

                        // 2. Disparar clic en 'AutoselecciÃ³n' para mover los lotes a 'Lotes seleccionados'
                        oItemAuto.Click(BoCellClickType.ct_Regular);

                        System.Windows.Forms.Application.DoEvents();
                        Thread.Sleep(40);

                        processed++;
                    }
                    catch (Exception exRow)
                    {
                        Console.WriteLine(string.Format("Fila {0} fallÃ³: {1}", row, exRow.Message));
                    }
                }

                _sboApp.StatusBar.SetText(
                    string.Format("Â¡Lotes asignados en {0} de {1} lÃ­neas! Confirmando...", processed, totalRows),
                    BoMessageTime.bmt_Short,
                    BoStatusBarMessageType.smt_Success
                );

                // 3. Confirmar con el botÃ³n 'OK' (Item 1) para guardar los lotes y cerrar la ventana
                if (_autoConfirm)
                {
                    try
                    {
                        Thread.Sleep(150);
                        System.Windows.Forms.Application.DoEvents();
                        oForm.Items.Item("1").Click(BoCellClickType.ct_Regular);
                    }
                    catch (Exception exOk)
                    {
                        Console.WriteLine("Error al confirmar con OK: " + exOk.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _sboApp.StatusBar.SetText("Error al autoasignar lotes: " + ex.Message, BoMessageTime.bmt_Long, BoStatusBarMessageType.smt_Error);
            }
        }
    }
}

