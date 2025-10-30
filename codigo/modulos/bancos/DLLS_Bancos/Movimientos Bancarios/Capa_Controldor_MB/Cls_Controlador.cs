using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using Capa_Modelo_MB;

namespace Capa_Controldor_MB
{
    public class Cls_Controlador
    {
        private readonly Cls_Sentencias cn = new Cls_Sentencias();
        private readonly Cls_CRUD crud = new Cls_CRUD();

        public class Cls_CuadreCaja
        {
            public decimal deSaldoInicial { get; set; }
            public decimal deTotalIngresos { get; set; }
            public decimal deTotalEgresos { get; set; }
            public decimal deSaldoFinal { get; set; }
            public decimal deSaldoReal { get; set; }
            public decimal deDiferencia { get; set; }
            public bool bCuadra { get; set; }
            public DataTable dts_Movimientos { get; set; }
        }

        public DataTable fun_CargarCuentas()
        {
            return crud.fun_ObtenerCuentas();
        }

        public string fun_ObtenerNombreCuenta(int iIdCuenta)
        {
            return crud.fun_ObtenerNombreCuenta(iIdCuenta);
        }

        public DataTable fun_ObtenerOperaciones()
        {
            return crud.fun_ObtenerOperaciones();
        }

        public string fun_ObtenerSignoOperacion(int iIdOperacion)
        {
            return crud.fun_ObtenerSignoOperacionPorId(iIdOperacion);
        }

        public bool fun_RequiereCuentaDestino(string sNombreOperacion)
        {
            if (string.IsNullOrWhiteSpace(sNombreOperacion))
                return false;

            return sNombreOperacion.Equals("TRANSFERENCIA_ENVIADA", StringComparison.OrdinalIgnoreCase) ||
                   sNombreOperacion.Equals("TRANSFERENCIA_RECIBIDA", StringComparison.OrdinalIgnoreCase);
        }

        public (bool bOk, string sMensaje) fun_ValidarMovimientoCompleto(
            Cls_Sentencias mov,
            List<Cls_Sentencias.Cls_MovimientoDetalle> lst_Detalles)
        {
            try
            {
                // Validaciones básicas
                if (mov.iFk_Id_cuenta_origen <= 0)
                    return (false, "Seleccione cuenta ORIGEN.");

                if (mov.iFk_Id_operacion <= 0)
                    return (false, "Seleccione la OPERACIÓN.");

                if (mov.deCmp_valor_total <= 0)
                    return (false, "El monto total debe ser mayor a cero.");

                // Validar detalles
                if (lst_Detalles == null || lst_Detalles.Count == 0)
                    return (false, "Debe agregar al menos UN detalle.");

                decimal deSumaDetalles = 0;
                foreach (var detalle in lst_Detalles)
                {
                    if (detalle.deCmp_valor <= 0)
                        return (false, "Todos los montos de detalle deben ser > 0.");

                    if (string.IsNullOrWhiteSpace(detalle.sFk_Id_cuenta_contable))
                        return (false, "Todos los detalles deben tener cuenta contable.");

                    deSumaDetalles += detalle.deCmp_valor;
                }

                // Validar que la suma de detalles coincida con el monto total
                if (Math.Abs(deSumaDetalles - mov.deCmp_valor_total) > 0.01m)
                    return (false, $"La suma de detalles ({deSumaDetalles:C2}) no coincide con el monto total ({mov.deCmp_valor_total:C2}).");

                return (true, "OK");
            }
            catch (Exception ex)
            {
                return (false, $"Error en validación: {ex.Message}");
            }
        }

        public int fun_GuardarMovimiento(Cls_Sentencias mov, List<Cls_Sentencias.Cls_MovimientoDetalle> lst_Detalles)
        {
            return crud.fun_CrearMovimientoConDetalles(mov, lst_Detalles);
        }

        public List<string> fun_ValidarMovimiento(Cls_Sentencias mov_Movimiento, List<Cls_Sentencias.Cls_MovimientoDetalle> lst_Detalles)
        {
            List<string> lst_Errores = new List<string>();

            if (mov_Movimiento.iFk_Id_cuenta_origen <= 0)
                lst_Errores.Add("La cuenta origen es requerida");
            if (mov_Movimiento.iFk_Id_operacion <= 0)
                lst_Errores.Add("La operación bancaria es requerida");
            if (mov_Movimiento.deCmp_valor_total <= 0)
                lst_Errores.Add("El monto total debe ser mayor a cero");

            return lst_Errores;
        }

        public Cls_Sentencias.Cls_MovimientoDetalle fun_CrearDetallePrincipal(decimal deMonto, string sNumeroDocumento = null, string sDescripcion = null)
        {
            return new Cls_Sentencias.Cls_MovimientoDetalle
            {
                iFk_Id_tipo_pago = null,
                sCmp_Num_Documento = sNumeroDocumento,
                deCmp_Monto = deMonto,
                sCmp_Descripcion = sDescripcion ?? "Movimiento principal",
                iCmp_Conciliado = 0
            };
        }

        public DataTable fun_ObtenerMovimientosBancarios()
        {
            try
            {
                return crud.fun_ObtenerDetallesContablesParaCaptura();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error en controlador al obtener movimientos: {ex.Message}");
            }
        }

        public DataTable fun_ObtenerDetallesContables()
        {
            try
            {
                return crud.fun_ObtenerDetallesContables();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error en controlador al obtener detalles contables: {ex.Message}");
            }
        }

        public string fun_ValidarDetalles(List<Cls_Sentencias.Cls_MovimientoDetalle> lst_Detalles)
        {
            if (lst_Detalles == null || lst_Detalles.Count == 0)
                return "Debe agregar al menos un detalle con monto mayor a cero.";

            foreach (Cls_Sentencias.Cls_MovimientoDetalle det in lst_Detalles)
            {
                if (det.deCmp_Monto <= 0)
                    return "Cada detalle debe tener un monto mayor a cero.";
            }
            return null;
        }
    }

        // balance 
        
        
    }
