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

        public const string ButtonUid = "btnAutoAll";

        public BatchAutoAssigner(SAPbouiCOM.Application sboApp)
        {
            _sboApp = sboApp;

            _buttonCaption = ConfigurationManager.AppSettings["ButtonCaption"] ?? "Autoasignar Todo";

            int width;
            if (!int.TryParse(ConfigurationManager.AppSettings["ButtonWidth"], out width))
                width = 140;
            _buttonWidth = width;

            // Por defecto activamos auto-confirmación para que guarde con OK automáticamente
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
                Console.WriteLine("Error al agregar botón Autoasignar: " + ex.Message);
            }
        }

        // SAP reubica sus ítems nativos al redimensionar, pero no los agregados por add-on.
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
                string cap = (((SAPbouiCOM.Button)itm.Specific).Caption ?? "").ToLower();
                return cap.Contains("auto");
            }
            catch { return false; }
        }

        // Busca el botón nativo de Selección automática por caption (no confiar en un UID fijo).
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
            try
            {
                // Matriz 3: Líneas de documentos
                Matrix oMatrix = null;
                try
                {
                    oMatrix = (Matrix)oForm.Items.Item("3").Specific;
                }
                catch
                {
                    _sboApp.StatusBar.SetText("No se encontró la matriz de artículos del documento (Item 3).", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
                    return;
                }

                int totalRows = oMatrix.RowCount;
                if (totalRows == 0)
                {
                    _sboApp.StatusBar.SetText("No hay artículos en la lista para asignar.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
                    return;
                }

                // Localizar el botón de Autoselección nativo
                Item oItemAuto = FindNativeAutoButton(oForm);

                if (oItemAuto == null)
                {
                    _sboApp.StatusBar.SetText("No se encontró el botón nativo de Autoselección.", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
                    return;
                }

                _sboApp.SetStatusBarMessage("Iniciando autoasignación masiva de lotes...", BoMessageTime.bmt_Short, false);

                int processed = 0;

                // Recorrer cada fila de la matriz
                for (int row = 1; row <= totalRows; row++)
                {
                    try
                    {
                        // 1. Seleccionar y hacer clic en la fila para que SAP active ese ítem y cargue sus lotes disponibles
                        try { oMatrix.SelectRow(row, true, false); } catch { }
                        try { oMatrix.Columns.Item(0).Cells.Item(row).Click(BoCellClickType.ct_Regular); } catch { }
                        try { oMatrix.Columns.Item(1).Cells.Item(row).Click(BoCellClickType.ct_Regular); } catch { }

                        // Dar tiempo a la interfaz de SAP para sincronizar los lotes de la línea
                        System.Windows.Forms.Application.DoEvents();
                        Thread.Sleep(60);

                        // 2. Disparar clic en 'Autoselección' para mover los lotes a 'Lotes seleccionados'
                        oItemAuto.Click(BoCellClickType.ct_Regular);

                        System.Windows.Forms.Application.DoEvents();
                        Thread.Sleep(40);

                        processed++;
                    }
                    catch (Exception exRow)
                    {
                        Console.WriteLine(string.Format("Fila {0} falló: {1}", row, exRow.Message));
                    }
                }

                _sboApp.StatusBar.SetText(
                    string.Format("¡Lotes asignados en {0} de {1} líneas! Confirmando...", processed, totalRows),
                    BoMessageTime.bmt_Short,
                    BoStatusBarMessageType.smt_Success
                );

                // 3. Confirmar con el botón 'OK' (Item 1) para guardar los lotes y cerrar la ventana
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
