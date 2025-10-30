using System;
using System.Data;
using System.Data.Odbc;
using System.Collections.Generic;

namespace Capa_Modelo_MB
{
    public class Cls_CRUD
    {
        private Cls_Conexion cn = new Cls_Conexion();

        public DataTable fun_ObtenerCuentas()
        {
            try
            {
                string sSql = @"SELECT 
                            Pk_Id_CuentaBancaria AS Pk_Id_Cuenta, 
                            Cmp_NumeroCuenta AS Cmp_Numero_Cuenta,
                            CONCAT(b.Cmp_NombreBanco, ' - ', cb.Cmp_NumeroCuenta) AS Cmp_Nombre_Cuenta
                       FROM Tbl_CuentasBancarias cb
                       INNER JOIN Tbl_Bancos b ON cb.Fk_Id_Banco = b.Pk_Id_Banco
                       WHERE cb.Cmp_Estado = 1";

                OdbcDataAdapter oda_Adapter = new OdbcDataAdapter(sSql, cn.fun_ConexionBD());
                DataTable dts_Resultado = new DataTable();
                oda_Adapter.Fill(dts_Resultado);
                return dts_Resultado;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener las cuentas: " + ex.Message);
            }
        }

        public string fun_ObtenerNombreCuenta(int iIdCuenta)
        {
            string sNombreCuenta = "";
            try
            {
                string sSql = @"SELECT CONCAT(b.Cmp_NombreBanco, ' - ', cb.Cmp_NumeroCuenta)
                       FROM Tbl_CuentasBancarias cb
                       INNER JOIN Tbl_Bancos b ON cb.Fk_Id_Banco = b.Pk_Id_Banco
                       WHERE cb.Pk_Id_CuentaBancaria = ?";

                using (OdbcConnection odcn_Conn = cn.fun_ConexionBD())
                {
                    using (OdbcCommand odc_Cmd = new OdbcCommand(sSql, odcn_Conn))
                    {
                        odc_Cmd.Parameters.AddWithValue("@Pk_Id_Cuenta", iIdCuenta);
                        object oResultado = odc_Cmd.ExecuteScalar();

                        if (oResultado != null && oResultado != DBNull.Value)
                            sNombreCuenta = oResultado.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener el nombre de la cuenta: " + ex.Message);
            }
            return sNombreCuenta;
        }

        public DataTable fun_ObtenerOperaciones()
        {
            try
            {
                string sSql = @"SELECT 
                    Pk_Id_Transaccion AS Pk_Id_operacion, 
                    Cmp_NombreTransaccion AS Cmp_nombre
                   FROM Tbl_TransaccionesBancarias
                   WHERE Cmp_Estado = 1";

                OdbcDataAdapter oda_Adapter = new OdbcDataAdapter(sSql, cn.fun_ConexionBD());
                DataTable dts_Resultado = new DataTable();
                oda_Adapter.Fill(dts_Resultado);
                return dts_Resultado;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener las operaciones bancarias: " + ex.Message);
            }
        }

        public DataTable fun_ObtenerTiposPago()
        {
            try
            {
                string sSql = @"SELECT 
                    Pk_Id_TipoPago AS Pk_Id_TipoPago, 
                    Cmp_NombreTipoPago AS Cmp_nombre
                   FROM Tbl_TiposPago
                   WHERE Cmp_Estado = 1";

                OdbcDataAdapter oda_Adapter = new OdbcDataAdapter(sSql, cn.fun_ConexionBD());
                DataTable dts_Resultado = new DataTable();
                oda_Adapter.Fill(dts_Resultado);
                return dts_Resultado;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener los tipos de pago: " + ex.Message);
            }
        }

        public string fun_ObtenerSignoOperacionPorId(int iIdOperacion)
        {
            string sSigno = "";
            try
            {
                string sSql = @"SELECT Cmp_Efecto 
                       FROM Tbl_TransaccionesBancarias
                       WHERE Pk_Id_Transaccion = ? AND Cmp_Estado = 1";

                using (OdbcConnection odcn_Conn = cn.fun_ConexionBD())
                using (OdbcCommand odc_Cmd = new OdbcCommand(sSql, odcn_Conn))
                {
                    odc_Cmd.Parameters.AddWithValue("@id", iIdOperacion);
                    object oResultado = odc_Cmd.ExecuteScalar();
                    if (oResultado != null && oResultado != DBNull.Value)
                    {
                        string sEfecto = oResultado.ToString();
                        sSigno = sEfecto == "POSITIVO" ? "+" : "-";
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener el signo de la operación: " + ex.Message);
            }
            return sSigno;
        }

        private string fun_Trunc(string sCadena, int iMaxLongitud)
        {
            if (string.IsNullOrWhiteSpace(sCadena)) return null;
            return sCadena.Length <= iMaxLongitud ? sCadena : sCadena.Substring(0, iMaxLongitud);
        }

        public int fun_CrearMovimientoConDetalles(Cls_Sentencias mov, List<Cls_Sentencias.Cls_MovimientoDetalle> lst_Detalles)
        {
            if (mov == null) throw new ArgumentNullException(nameof(mov));
            if (lst_Detalles == null) lst_Detalles = new List<Cls_Sentencias.Cls_MovimientoDetalle>();

            // Validaciones antes de la transacción
            if (mov.iFk_Id_cuenta_origen <= 0)
                throw new Exception("La cuenta origen es requerida");

            if (mov.iFk_Id_operacion <= 0)
                throw new Exception("La operación es requerida");

            // Truncar campos para cumplir con longitudes máximas
            mov.sCmp_numero_documento = fun_Trunc(mov.sCmp_numero_documento, 50);
            mov.sCmp_concepto = fun_Trunc(mov.sCmp_concepto, 255);
            mov.sCmp_beneficiario = fun_Trunc(mov.sCmp_beneficiario, 255);
            mov.sCmp_estado = fun_Trunc(string.IsNullOrWhiteSpace(mov.sCmp_estado) ? "ACTIVO" : mov.sCmp_estado, 20);

            using (OdbcConnection odcn_Conn = cn.fun_ConexionBD())
            using (OdbcTransaction odc_Transaccion = odcn_Conn.BeginTransaction())
            {
                try
                {
                    // Obtener el próximo ID de movimiento de forma más segura
                    int iProximoIdMovimiento = 1;
                    string sSqlMaxId = @"
                SELECT COALESCE(MAX(Pk_Id_Movimiento), 0) + 1 
                FROM Tbl_MovimientoBancarioEncabezado 
                WHERE Fk_Id_CuentaOrigen = ? AND Fk_Id_Operacion = ?";

                    using (OdbcCommand odc_CmdMax = new OdbcCommand(sSqlMaxId, odcn_Conn, odc_Transaccion))
                    {
                        odc_CmdMax.Parameters.Add(new OdbcParameter("@CuentaOrigen", OdbcType.Int) { Value = mov.iFk_Id_cuenta_origen });
                        odc_CmdMax.Parameters.Add(new OdbcParameter("@Operacion", OdbcType.Int) { Value = mov.iFk_Id_operacion });

                        object oResultado = odc_CmdMax.ExecuteScalar();
                        if (oResultado != null && oResultado != DBNull.Value)
                            iProximoIdMovimiento = Convert.ToInt32(oResultado);
                    }

                    // Insertar en encabezado de movimiento con parámetros explícitos
                    string sSqlMov = @"
                INSERT INTO Tbl_MovimientoBancarioEncabezado
                (Pk_Id_Movimiento, Fk_Id_CuentaOrigen, Fk_Id_Operacion, 
                 Cmp_NumeroDocumento, Cmp_Fecha, Cmp_Concepto, Cmp_MontoTotal,
                 Fk_Id_TipoPago, Fk_Id_CuentaDestino, Cmp_Beneficiario, Cmp_Estado, 
                 Cmp_Conciliado, Cmp_UsuarioRegistro)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                    using (OdbcCommand odc_Cmd = new OdbcCommand(sSqlMov, odcn_Conn, odc_Transaccion))
                    {
                        // Agregar parámetros de forma explícita
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Pk_Id_Movimiento", OdbcType.Int) { Value = iProximoIdMovimiento });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Fk_Id_CuentaOrigen", OdbcType.Int) { Value = mov.iFk_Id_cuenta_origen });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Fk_Id_Operacion", OdbcType.Int) { Value = mov.iFk_Id_operacion });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_NumeroDocumento", OdbcType.VarChar, 50) { Value = (object)mov.sCmp_numero_documento ?? DBNull.Value });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_Fecha", OdbcType.Date) { Value = mov.dCmp_fecha_movimiento });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_Concepto", OdbcType.VarChar, 255) { Value = (object)mov.sCmp_concepto ?? DBNull.Value });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_MontoTotal", OdbcType.Decimal) { Value = mov.deCmp_valor_total });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Fk_Id_TipoPago", OdbcType.Int) { Value = (object)mov.iFk_Id_tipo_pago ?? DBNull.Value });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Fk_Id_CuentaDestino", OdbcType.Int) { Value = (object)mov.iFk_Id_cuenta_destino ?? DBNull.Value });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_Beneficiario", OdbcType.VarChar, 255) { Value = (object)mov.sCmp_beneficiario ?? DBNull.Value });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_Estado", OdbcType.VarChar, 20) { Value = mov.sCmp_estado });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_Conciliado", OdbcType.Int) { Value = mov.iCmp_conciliado });
                        odc_Cmd.Parameters.Add(new OdbcParameter("@Cmp_UsuarioRegistro", OdbcType.VarChar, 50) { Value = mov.sCmp_usuario_registro ?? "SISTEMA" });

                        int iFilasAfectadas = odc_Cmd.ExecuteNonQuery();

                        if (iFilasAfectadas == 0)
                            throw new Exception("No se pudo insertar el movimiento encabezado");

                        // Asignar los IDs generados al objeto movimiento
                        mov.iPk_Id_movimiento = iProximoIdMovimiento;
                    }

                    // Insertar detalles contables
                    if (lst_Detalles.Count > 0)
                    {
                        // Obtener el próximo ID de detalle
                        int iProximoIdDetalle = 1;
                        string sSqlMaxDetalle = @"
                    SELECT COALESCE(MAX(Pk_Id_Detalle), 0) + 1 
                    FROM Tbl_MovimientoBancarioDetalle 
                    WHERE Fk_Id_Movimiento = ? AND Fk_Id_CuentaOrigen = ? AND Fk_Id_Operacion = ?";

                        using (OdbcCommand odc_CmdMaxDet = new OdbcCommand(sSqlMaxDetalle, odcn_Conn, odc_Transaccion))
                        {
                            odc_CmdMaxDet.Parameters.Add(new OdbcParameter("@Movimiento", OdbcType.Int) { Value = mov.iPk_Id_movimiento });
                            odc_CmdMaxDet.Parameters.Add(new OdbcParameter("@CuentaOrigen", OdbcType.Int) { Value = mov.iFk_Id_cuenta_origen });
                            odc_CmdMaxDet.Parameters.Add(new OdbcParameter("@Operacion", OdbcType.Int) { Value = mov.iFk_Id_operacion });

                            object oResultado = odc_CmdMaxDet.ExecuteScalar();
                            if (oResultado != null && oResultado != DBNull.Value)
                                iProximoIdDetalle = Convert.ToInt32(oResultado);
                        }

                        string sSqlDet = @"
                    INSERT INTO Tbl_MovimientoBancarioDetalle
                    (Fk_Id_Movimiento, Fk_Id_CuentaOrigen, Fk_Id_Operacion, Pk_Id_Detalle,
                     Fk_Id_CuentaContable, Cmp_TipoOperacion, Cmp_Valor, Cmp_Descripcion,
                     Cmp_OrdenDetalle, Cmp_UsuarioRegistro)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                        using (OdbcCommand odc_CmdDet = new OdbcCommand(sSqlDet, odcn_Conn, odc_Transaccion))
                        {
                            int iOrden = 1;
                            foreach (Cls_Sentencias.Cls_MovimientoDetalle det in lst_Detalles)
                            {
                                det.sCmp_Descripcion = fun_Trunc(det.sCmp_Descripcion, 255);

                                odc_CmdDet.Parameters.Clear();

                                // Agregar parámetros de forma explícita
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Fk_Id_Movimiento", OdbcType.Int) { Value = mov.iPk_Id_movimiento });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Fk_Id_CuentaOrigen", OdbcType.Int) { Value = mov.iFk_Id_cuenta_origen });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Fk_Id_Operacion", OdbcType.Int) { Value = mov.iFk_Id_operacion });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Pk_Id_Detalle", OdbcType.Int) { Value = iProximoIdDetalle });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Fk_Id_CuentaContable", OdbcType.VarChar, 20) { Value = det.sFk_Id_cuenta_contable ?? "1110" });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Cmp_TipoOperacion", OdbcType.VarChar, 1) { Value = det.sCmp_tipo_operacion ?? "C" });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Cmp_Valor", OdbcType.Decimal) { Value = det.deCmp_valor });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Cmp_Descripcion", OdbcType.VarChar, 255) { Value = (object)det.sCmp_Descripcion ?? DBNull.Value });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Cmp_OrdenDetalle", OdbcType.Int) { Value = iOrden });
                                odc_CmdDet.Parameters.Add(new OdbcParameter("@Cmp_UsuarioRegistro", OdbcType.VarChar, 50) { Value = mov.sCmp_usuario_registro ?? "SISTEMA" });

                                int iFilasDetalle = odc_CmdDet.ExecuteNonQuery();

                                if (iFilasDetalle == 0)
                                    throw new Exception($"No se pudo insertar el detalle en orden {iOrden}");

                                iProximoIdDetalle++;
                                iOrden++;
                            }
                        }
                    }

                    odc_Transaccion.Commit();
                    return mov.iPk_Id_movimiento;
                }
                catch (Exception ex)
                {
                    odc_Transaccion.Rollback();
                    throw new Exception($"Error al crear movimiento con detalles: {ex.Message}");
                }
            }
        }

        public List<string> fun_ObtenerEstadosMovimiento()
        {
            List<string> lst_Estados = new List<string>();

            try
            {
                // Como Cmp_Estado es VARCHAR, obtenemos los valores distintos de la tabla
                string sSql = @"
                    SELECT DISTINCT Cmp_Estado 
                    FROM Tbl_MovimientoBancarioEncabezado 
                    WHERE Cmp_Estado IS NOT NULL 
                    ORDER BY Cmp_Estado";

                using (OdbcConnection odcn_Conn = cn.fun_ConexionBD())
                using (OdbcCommand odc_Cmd = new OdbcCommand(sSql, odcn_Conn))
                {
                    using (OdbcDataReader odr_Reader = odc_Cmd.ExecuteReader())
                    {
                        while (odr_Reader.Read())
                        {
                            string sEstado = odr_Reader.GetString(0);
                            if (!string.IsNullOrEmpty(sEstado))
                                lst_Estados.Add(sEstado);
                        }
                    }
                }

                // Si no hay estados, agregamos los básicos
                if (lst_Estados.Count == 0)
                {
                    lst_Estados.AddRange(new[] { "ACTIVO", "ANULADO", "PENDIENTE" });
                }
            }
            catch (Exception ex)
            {
                lst_Estados = new List<string> { "ACTIVO", "ANULADO", "PENDIENTE" };
                Console.WriteLine($"Error al obtener estados: {ex.Message}. Usando valores por defecto.");
            }

            return lst_Estados;
        }

        public DataTable fun_ObtenerDetallesContablesParaCaptura()
        {
            try
            {
                string sQuery = @"
                    SELECT 
                        NULL AS Fk_Id_tipo_pago,
                        mbe.Cmp_NumeroDocumento AS Cmp_Num_Documento,
                        CASE 
                            WHEN mbd.Cmp_TipoOperacion = 'D' THEN mbd.Cmp_Valor 
                            ELSE 0 
                        END AS Debe,
                        CASE 
                            WHEN mbd.Cmp_TipoOperacion = 'C' THEN mbd.Cmp_Valor 
                            ELSE 0 
                        END AS Haber,
                        CONCAT('Cuenta contable: ', mbd.Fk_Id_CuentaContable, ' - ', mbd.Cmp_Descripcion) AS Cmp_Concepto
                    FROM Tbl_MovimientoBancarioDetalle mbd
                    INNER JOIN Tbl_MovimientoBancarioEncabezado mbe ON 
                        mbd.Fk_Id_Movimiento = mbe.Pk_Id_Movimiento AND 
                        mbd.Fk_Id_CuentaOrigen = mbe.Fk_Id_CuentaOrigen AND 
                        mbd.Fk_Id_Operacion = mbe.Fk_Id_Operacion
                    ORDER BY mbe.Pk_Id_Movimiento DESC, mbd.Cmp_OrdenDetalle";

                OdbcDataAdapter oda_Adapter = new OdbcDataAdapter(sQuery, cn.fun_ConexionBD());
                DataTable dts_Resultado = new DataTable();
                oda_Adapter.Fill(dts_Resultado);
                return dts_Resultado;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener movimientos bancarios: " + ex.Message);
            }
        }

        public string fun_ObtenerCuentaContablePorDefecto()
        {
            try
            {
                string sSql = @"SELECT Cmp_Valor 
                       FROM Tbl_ParametrosCheques 
                       WHERE Cmp_Parametro = 'CUENTA_BANCO_PRINCIPAL' 
                       AND Cmp_Estado = 1";
                
                using (OdbcConnection odcn_Conn = new Cls_Conexion().fun_ConexionBD())
                using (OdbcCommand odc_Cmd = new OdbcCommand(sSql, odcn_Conn))
                {
                    object oResultado = odc_Cmd.ExecuteScalar();
                    return oResultado != null ? oResultado.ToString() : "1110";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener cuenta contable por defecto: {ex.Message}");
                return "1110";
            }
        }

        public string fun_ObtenerTipoLineaPorSigno(string sSignoOperacion)
        {
            return sSignoOperacion == "-" ? "D" : "C";
        }

        public DataTable fun_ObtenerDetallesContables()
        {
            try
            {
                string sQuery = @"
                    SELECT 
                        mbd.Pk_Id_Detalle as ID_Detalle,
                        mbd.Fk_Id_Movimiento as ID_Movimiento,
                        mbd.Fk_Id_CuentaContable as Cuenta_Contable,
                        mbd.Cmp_TipoOperacion as Tipo_Linea,
                        mbd.Cmp_Valor as Valor,
                        mbe.Cmp_NumeroDocumento as Documento,
                        mbe.Cmp_Fecha as Fecha_Movimiento,
                        cc.Cmp_NombreCuenta as Nombre_Cuenta,
                        mbd.Cmp_Descripcion as Descripcion
                    FROM Tbl_MovimientoBancarioDetalle mbd
                    INNER JOIN Tbl_MovimientoBancarioEncabezado mbe ON 
                        mbd.Fk_Id_Movimiento = mbe.Pk_Id_Movimiento AND 
                        mbd.Fk_Id_CuentaOrigen = mbe.Fk_Id_CuentaOrigen AND 
                        mbd.Fk_Id_Operacion = mbe.Fk_Id_Operacion
                    INNER JOIN Tbl_CatalogoCuentas cc ON mbd.Fk_Id_CuentaContable = cc.Pk_Id_CuentaContable
                    ORDER BY mbd.Fk_Id_Movimiento DESC, mbd.Cmp_OrdenDetalle";

                OdbcDataAdapter oda_Adapter = new OdbcDataAdapter(sQuery, cn.fun_ConexionBD());
                DataTable dts_Resultado = new DataTable();
                oda_Adapter.Fill(dts_Resultado);
                return dts_Resultado;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener detalles contables: " + ex.Message);
            }
        }

        public DataTable fun_ObtenerCuentasContables()
        {
            try
            {
                string sSql = @"SELECT 
                            Pk_Id_CuentaContable, 
                            CONCAT(Pk_Id_CuentaContable, ' - ', Cmp_NombreCuenta) AS Cmp_NombreCompleto
                       FROM Tbl_CatalogoCuentas
                       WHERE Cmp_Estado = 1
                       ORDER BY Pk_Id_CuentaContable";

                OdbcDataAdapter oda_Adapter = new OdbcDataAdapter(sSql, cn.fun_ConexionBD());
                DataTable dts_Resultado = new DataTable();
                oda_Adapter.Fill(dts_Resultado);
                return dts_Resultado;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener las cuentas contables: " + ex.Message);
            }
        }

        public DataTable fun_ObtenerMovimientosPorFiltro(int? iIdCuenta = null, DateTime? dFechaDesde = null, DateTime? dFechaHasta = null, string sEstado = null)
        {
            try
            {
                string sSql = @"
                    SELECT 
                        mbe.Pk_Id_Movimiento,
                        mbe.Fk_Id_CuentaOrigen,
                        mbe.Fk_Id_Operacion,
                        cb.Cmp_NumeroCuenta,
                        b.Cmp_NombreBanco,
                        tb.Cmp_NombreTransaccion,
                        mbe.Cmp_NumeroDocumento,
                        mbe.Cmp_Fecha,
                        mbe.Cmp_MontoTotal,
                        mbe.Cmp_Beneficiario,
                        mbe.Cmp_Estado,
                        mbe.Cmp_Conciliado
                    FROM Tbl_MovimientoBancarioEncabezado mbe
                    INNER JOIN Tbl_CuentasBancarias cb ON mbe.Fk_Id_CuentaOrigen = cb.Pk_Id_CuentaBancaria
                    INNER JOIN Tbl_Bancos b ON cb.Fk_Id_Banco = b.Pk_Id_Banco
                    INNER JOIN Tbl_TransaccionesBancarias tb ON mbe.Fk_Id_Operacion = tb.Pk_Id_Transaccion
                    WHERE 1=1";

                List<OdbcParameter> lst_Parametros = new List<OdbcParameter>();

                if (iIdCuenta.HasValue)
                {
                    sSql += " AND mbe.Fk_Id_CuentaOrigen = ?";
                    lst_Parametros.Add(new OdbcParameter("@Cuenta", iIdCuenta.Value));
                }

                if (dFechaDesde.HasValue)
                {
                    sSql += " AND mbe.Cmp_Fecha >= ?";
                    lst_Parametros.Add(new OdbcParameter("@FechaDesde", dFechaDesde.Value));
                }

                if (dFechaHasta.HasValue)
                {
                    sSql += " AND mbe.Cmp_Fecha <= ?";
                    lst_Parametros.Add(new OdbcParameter("@FechaHasta", dFechaHasta.Value));
                }

                if (!string.IsNullOrEmpty(sEstado))
                {
                    sSql += " AND mbe.Cmp_Estado = ?";
                    lst_Parametros.Add(new OdbcParameter("@Estado", sEstado));
                }

                sSql += " ORDER BY mbe.Cmp_Fecha DESC, mbe.Pk_Id_Movimiento DESC";

                OdbcDataAdapter oda_Adapter = new OdbcDataAdapter(sSql, cn.fun_ConexionBD());
                foreach (var param in lst_Parametros)
                {
                    oda_Adapter.SelectCommand.Parameters.Add(param);
                }

                DataTable dts_Resultado = new DataTable();
                oda_Adapter.Fill(dts_Resultado);
                return dts_Resultado;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener movimientos: " + ex.Message);
            }
        }



        // CUADRE DE CAJA
       


    }
}