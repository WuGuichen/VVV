"""Authoring colors; physical profiles come from the existing shared catalog."""
COLORS={
    'graphite':((49,57,63),11),'panel':((80,91,98),11),
    'paint_white':((185,185,167),11),'red':((158,56,40),11),
    'rubber':((27,30,31),10),'steel':((107,120,124),12),
    'glass':((35,58,70),14),'concrete':((166,166,150),10),
    'yellow':((219,164,45),11),'rust':((113,73,49),10),
}
LABELS={'graphite':'车架石墨灰','panel':'建筑蓝灰','paint_white':'暖灰白漆','red':'识别红漆',
        'rubber':'轮胎橡胶','steel':'裸露钢材','glass':'驾驶舱玻璃','concrete':'暖灰混凝土',
        'yellow':'安全黄漆','rust':'氧化铁锈'}
SAMPLES=(('Veh_Truck_FuelTanker','油罐车'),('Arch_Hangar_MainBay','主车库'),
         ('Sec_Wall_Straight_Long','围墙'),('Prop_Cabinet_Tool_Mobile','工具柜'),
         ('Sec_Barrier_Jersey_Concrete_01','混凝土路障'))
