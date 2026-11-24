using System;
using System.Collections.Generic;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Simulation;

namespace Game.Core.Staff
{
    /// <summary>Comportamento do funcionário (tipo de trabalho, não conteúdo). Novos papéis na Etapa 9.</summary>
    public enum StaffRole
    {
        /// <summary>Passa itens no caixa sozinho.</summary>
        Cashier = 0,
    }

    public sealed class StaffDef
    {
        public StaffDef(string id, string name, StaffRole role, long salary, long hireCost, double secondsPerAction, int maxHired)
        {
            Id = id; Name = string.IsNullOrEmpty(name) ? id : name; Role = role; Salary = salary;
            HireCost = hireCost; SecondsPerAction = secondsPerAction; MaxHired = maxHired;
        }

        public string Id { get; }
        public string Name { get; }
        public StaffRole Role { get; }
        /// <summary>Debitado a cada <c>SalaryIntervalSeconds</c> de jogo online.</summary>
        public long Salary { get; }
        public long HireCost { get; }
        public double SecondsPerAction { get; }
        public int MaxHired { get; }
    }

    public sealed class StaffState
    {
        public List<EmployeeState> Employees { get; set; } = new List<EmployeeState>();

        /// <summary>Tempo de jogo online acumulado desde o último pagamento.</summary>
        public long SalaryElapsedMs { get; set; }
    }

    public sealed class EmployeeState
    {
        public string TypeId { get; set; }

        /// <summary>False quando o último salário não pôde ser pago: fica parado até o próximo pagamento.</summary>
        public bool Active { get; set; } = true;

        public long WorkProgressMs { get; set; }
    }

    public sealed class HireCommand : ICommand
    {
        public HireCommand(string staffTypeId) { StaffTypeId = staffTypeId; }
        public string StaffTypeId { get; }
    }

    public sealed class FireCommand : ICommand
    {
        public FireCommand(int employeeIndex) { EmployeeIndex = employeeIndex; }
        public int EmployeeIndex { get; }
    }

    public readonly struct SalaryPaid : IGameEvent
    {
        public SalaryPaid(long total, int unpaid) { Total = total; Unpaid = unpaid; }
        public long Total { get; }
        /// <summary>Quantos ficaram sem salário (e pararam).</summary>
        public int Unpaid { get; }
    }

    public static class StaffRules
    {
        public static int CountOf(StaffState staff, string typeId)
        {
            int n = 0;
            foreach (var e in staff.Employees) if (e.TypeId == typeId) n++;
            return n;
        }

        /// <summary>Folha de pagamento por tempo de jogo online. Sem saldo, o funcionário para (sem game over).</summary>
        public static void AccrueSalaries(GameContext ctx, long dtMs)
        {
            var staff = ctx.State.Staff;
            if (staff.Employees.Count == 0)
            {
                staff.SalaryElapsedMs = 0;
                return;
            }
            long interval = Math.Max(1, ctx.Catalog.Economy.SalaryIntervalSeconds * 1000L);
            staff.SalaryElapsedMs += dtMs;
            while (staff.SalaryElapsedMs >= interval)
            {
                staff.SalaryElapsedMs -= interval;
                long total = 0;
                int unpaid = 0;
                foreach (var e in staff.Employees)
                {
                    if (!ctx.Catalog.TryGetStaff(e.TypeId, out var def)) { e.Active = false; continue; }
                    bool paid = def.Salary <= 0 || ctx.Wallet.TrySpendCoins(def.Salary, "salario:" + def.Id);
                    e.Active = paid;
                    if (paid) total += def.Salary;
                    if (paid) ctx.State.Stats.SalariesPaid += def.Salary;
                    else unpaid++;
                }
                ctx.Events.Publish(new SalaryPaid(total, unpaid));
            }
        }
    }

    public sealed class HireHandler : ICommandHandler<HireCommand>
    {
        public CommandResult Validate(GameContext ctx, HireCommand c)
        {
            if (!ctx.Catalog.TryGetStaff(c.StaffTypeId, out var def)) return CommandResult.Fail(CommandErrors.InvalidArgument);
            if (StaffRules.CountOf(ctx.State.Staff, def.Id) >= def.MaxHired) return CommandResult.Fail(CommandErrors.MaxReached);
            return def.HireCost <= 0 || ctx.Wallet.CanAffordCoins(def.HireCost) ? CommandResult.Ok() : CommandResult.Fail(CommandErrors.InsufficientCoins);
        }

        public void Apply(GameContext ctx, HireCommand c)
        {
            var def = ctx.Catalog.GetStaff(c.StaffTypeId);
            if (def.HireCost > 0) ctx.Wallet.TrySpendCoins(def.HireCost, "contratacao:" + def.Id);
            ctx.State.Staff.Employees.Add(new EmployeeState { TypeId = def.Id, Active = true });
        }
    }

    public sealed class FireHandler : ICommandHandler<FireCommand>
    {
        public CommandResult Validate(GameContext ctx, FireCommand c) =>
            c.EmployeeIndex >= 0 && c.EmployeeIndex < ctx.State.Staff.Employees.Count
                ? CommandResult.Ok()
                : CommandResult.Fail(CommandErrors.InvalidArgument);

        public void Apply(GameContext ctx, FireCommand c) => ctx.State.Staff.Employees.RemoveAt(c.EmployeeIndex);
    }
}
