namespace RabbitMQ.Models.Enums;

[Flags]
public enum BatchStage
{
  None=0,
  Stage1=1,
  Stage2=2,
  Stage3=4,
  Stage4=8,
}