using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Contracts.ClassRequests;

/// <summary>
/// Pedido para aprovar um ClassRequest e criar a aula correspondente.
/// O RoomId é obrigatório (não é permitido aprovar sem sala).
/// </summary>
public sealed record ApproveClassRequest(Guid RoomId);
