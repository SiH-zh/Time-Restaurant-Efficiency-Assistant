using System;
using RestaurantBoost;
class TestRules
{
    static int n;
    static void Check(bool value, string name) { n++; if(!value) throw new Exception("验证失败："+name); }
    static void Main()
    {
        int x;
        foreach(var s in new[]{"1","10","20","30","40","50"}) Check(Rules.TryLimit(s,out x),"有效人数"+s);
        foreach(var s in new[]{"0","-1","51","120","1.5","NaN","Infinity","","2147483648"}) Check(!Rules.TryLimit(s,out x),"非法人数"+s);
        foreach(var s in new[]{"1.0","1.1","2","5.5","20.0"}) Check(Rules.TryUnits(s,10,200,out x),"倍率"+s);
        foreach(var s in new[]{"0.9","20.1","2.01","NaN","Infinity","1e2","1,5","","79228162514264337593543950335"}) Check(!Rules.TryUnits(s,10,200,out x),"非法倍率"+s);
        Check(Rules.TryUnits("0.1",1,100,out x)&&x==1,"最短间隔");
        Check(Rules.TryUnits("10.0",1,100,out x)&&x==100,"最长间隔");
        foreach(var s in new[]{"0","0.09","10.1","60","1.01","NaN","Infinity"}) Check(!Rules.TryUnits(s,1,100,out x),"非法到店间隔"+s);
        var p=new Preferences(); Check(!p.EmployeeEnabled&&!p.ArrivalEnabled&&!p.EatingEnabled&&!p.IncludePrivateRooms&&!p.TipCapacityEnabled&&!p.AutoCollectTips,"所有功能初装默认关闭");
        Check(p.TipCapacity==100000,"初装小费容量十万笔");
        for(int i=10;i<=200;i++) { p.EatingMultiplierUnits=i; Check(Math.Abs(Rules.Eating(15,p)-150f/i)<.00001,"用餐精度"+i); }
        for(int i=1;i<=100;i++) { p.ArrivalIntervalUnits=i; Check(Math.Abs(Rules.Arrival(30,p)-i/10f)<.000001,"到店间隔精度"+i); }
        foreach(int preset in new[]{50,20,10}) { p.ArrivalIntervalUnits=preset; Check(Rules.Arrival(30,p)==preset/10f,"到店预设"+preset); }
        p.EmployeeLimit=120; p.ArrivalIntervalUnits=0; p.EatingMultiplierUnits=-1;
        Rules.Normalize(p); Check(p.EmployeeLimit==20&&p.ArrivalIntervalUnits==10&&p.EatingMultiplierUnits==20,"配置越界回退");
        p.ArrivalIntervalUnits=600; p.ArrivalWindowX=float.NaN; p.ArrivalWindowY=float.PositiveInfinity; Rules.Normalize(p);
        Check(p.ArrivalIntervalUnits==10&&p.ArrivalWindowX==-24f&&p.ArrivalWindowY==-124f,"旧版越界间隔及非法悬浮位置回退");
        foreach(var s in new[]{"2000","5000","10000","12345","100000"}) Check(Rules.TryInteger(s,2000,100000,out x),"小费容量"+s);
        foreach(var s in new[]{"1999","100001","-1","5000.5","NaN","2147483648"}) Check(!Rules.TryInteger(s,2000,100000,out x),"非法小费容量"+s);
        Check(Rules.PreservedLoadLimit(2000,8000)==8000,"降低上限重读保留存量");
        Check(Rules.PreservedLoadLimit(10000,8000)==10000,"提高上限重读");
        Check(!Rules.CanAddTip(8000,2000)&&!Rules.CanAddTip(5000,5000)&&Rules.CanAddTip(4999,5000),"达到上限停止新增");
        foreach(var s in new[]{"5","30","60","3600"}) Check(Rules.TryInteger(s,5,3600,out x),"收取间隔"+s);
        foreach(var s in new[]{"0","4","3601","1.5","NaN"}) Check(!Rules.TryInteger(s,5,3600,out x),"非法间隔"+s);
        var clock=new CollectionClock();
        Check(!clock.Tick(true,true,"甲",30,100),"开启后不立即收取");
        Check(clock.Remaining(100)==30,"初始倒计时");
        Check(!clock.Tick(true,true,"甲",30,129.9),"未到间隔");
        Check(clock.Tick(true,true,"甲",30,130),"30秒触发一次");
        Check(!clock.Tick(true,true,"甲",30,130),"同一时刻不重复触发");
        Check(clock.Tick(true,true,"甲",30,1000),"长时间挂起恢复只尝试一次");
        Check(!clock.Tick(true,true,"甲",30,1000),"不补跑遗漏周期");
        Check(!clock.Tick(false,true,"甲",30,1010),"做客期间暂停");
        Check(!clock.Tick(true,true,"甲",30,1100),"返回重新计时");
        Check(!clock.Tick(true,true,"乙",30,1110),"切换存档重新计时");
        Check(!clock.Tick(true,true,"乙",60,1120),"修改为60秒重新计时");
        Check(!clock.Tick(true,true,"乙",60,1179),"60秒未到");
        Check(clock.Tick(true,true,"乙",60,1180),"60秒触发");
        Check(!clock.Tick(true,false,"乙",60,1200)&&clock.Remaining(1200)==0,"关闭后取消倒计时");
        p.TipCapacity=int.MaxValue; p.CollectIntervalSeconds=-1; Rules.Normalize(p);
        Check(p.TipCapacity==100000&&p.CollectIntervalSeconds==60,"小费非法配置回退");
        Check(!new Preferences().TipCapacityEnabled&&!new Preferences().AutoCollectTips,"小费初装关闭");
        Console.WriteLine("通过："+n+"项规则验证。");
    }
}
