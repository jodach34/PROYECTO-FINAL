# Cómo funciona Rescauta

Rescauta es una red que conecta alimentos disponibles con comedores comunitarios. Ordena la información de cada comedor, registra donaciones de insumos y permite consultar el avance de cada aporte con un código de seguimiento. No reemplaza el trabajo de los comedores: lo hace visible y lo conecta con más personas dispuestas a ayudar.

## El recorrido de una donación

1. El comedor se presenta. Comparte su ubicación de referencia, cuántas raciones prepara y qué insumos necesita. En esta versión, el alta de comedores requiere acompañamiento del equipo.

2. La red conoce la necesidad. El directorio muestra el estado de abastecimiento de cada comedor; los colores y porcentajes ayudan a priorizar dónde hace más falta el apoyo.

3. Una persona decide donar. Elige un comedor, un insumo y una cantidad. El formulario registra la donación en la API y genera un código único de seguimiento.

4. La entrega se puede consultar. Con el código, el donante consulta el estado de su aporte. La información visible corresponde solo a esa donación.

## Los roles de la red

Los comedores mantienen la información viva: actualizan inventario, revisan necesidades y confirman que una donación llegó. Los donantes eligen dónde aportar y reciben un código para conocer el avance. Los voluntarios apoyan en cocina, clasificación, traslado o difusión. Los operadores y coordinadores gestionan donaciones e inventario de los comedores que tienen asignados. Los administradores gestionan usuarios, roles y el alta de comedores.

## Seguimiento de una donación

Al registrar una donación recibes un código como DON-2026-00042. Ingrésalo en la página de seguimiento para consultar su estado: registrada, confirmada, en camino o entregada. El seguimiento es público para quien tenga el código y no muestra datos de otras personas. La donación suma stock cuando la coordinación confirma la recepción.

## Estado de abastecimiento

El estado se calcula con el inventario disponible frente a la capacidad de cada comedor. Se muestra abastecido desde 60%, alerta desde 30% y emergencia por debajo de 30%. Si el comedor no tiene insumos registrados se muestra "sin datos".

## Cuentas, roles y acceso

La plataforma tiene autenticación real. Una persona puede registrarse como donante desde el formulario público o solicitar el rol de operador. Las solicitudes de operador son revisadas por un administrador. Los permisos administrativos no se asignan desde el formulario público: los otorga un administrador existente. El inicio de sesión, el cierre de sesión, la recuperación de contraseña y el cambio de contraseña están disponibles.

## Qué está disponible hoy

El directorio público de comedores, el registro de donaciones de insumos, el seguimiento por código, la confirmación de recepción, el inventario, el Kardex, los reportes básicos de donaciones recibidas y stock disponible, y la administración de usuarios y roles forman parte de la versión actual.

## Qué sigue pendiente

El envío del formulario de contacto todavía no está conectado a un servicio. La plataforma tampoco procesa pagos ni donaciones monetarias: solo registra insumos. El alta de comedores requiere acompañamiento del equipo.
