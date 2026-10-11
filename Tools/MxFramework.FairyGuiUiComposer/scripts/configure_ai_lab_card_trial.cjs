"use strict";
// Authoring JSON only. Apply/Publish owns FairyGUI XML, package bytes and atlas output.
const fs = require("fs"), path = require("path");
const root = path.resolve(__dirname, "../../.."), lib = path.join(root, "ArtSource/UIKits/MxUiFoundation");
const prefix = "ui.component.runtime_ai_lab.", button = "ui.component.tactical.preparation.button";
const face = "ui.component.tactical.player_card_face", rowId = prefix + "card_trial_row", trialId = prefix + "card_trial";
const node = (name, type, x, y, width, height, extra = {}) => ({nodeId:name, name, type, order:0,
  bindingSlot:"", parentContainerId:"", visible:true,
  layout:{x,y,width,height,horizontalAnchor:"Left",verticalAnchor:"Top"},
  graph:null,text:null,image:null,component:null,list:null,...extra});
const text = (n,value,x,y,w,h,size=17,color="#ffe1dfd3") => node(n,"Text",x,y,w,h,
  {text:{value,font:"MxUI Sans SC",fontSize:size,color,align:"left",bold:false,singleLine:false,input:false,restrict:"",maxLength:0}});
const graph = (n,x,y,w,h) => node(n,"Graph",x,y,w,h,{graph:{fillColor:"#f5101c1b",cornerRadius:8}});
const component = (n,id,title,x,y,w,h) => node(n,"ComponentInstance",x,y,w,h,{component:{componentRecipeId:id,title,iconResourceId:""}});
const btn = (n,title,x,y,w,h=36) => component(n,button,title,x,y,w,h);
const list = (n,id,x,y,w,h,layout) => node(n,"List",x,y,w,h,
  {list:{itemComponentRecipeId:id,layout,lineGap:8,columnGap:12,selectionMode:"None",overflow:"Scroll",virtualized:true}});
const input = (n,value,x,y,w) => {const item=text(n,value,x,y,w,34,20);item.text={...item.text,input:true,singleLine:true,maxLength:11};return item;};
const composite = (id,name,w,h,nodes) => ({componentId:id,displayName:name,componentType:"Composite",
  fairyGuiComponentName:name,defaultSize:{width:w,height:h},resourceSlots:[],instanceProperties:[],statePresentation:null,
  template:{nodes:nodes.map((item,index)=>({...item,order:index}))}});
const recipes = [
  composite(rowId,"RuntimeAiLabCardTrialRow",442,96,[
    text("description","",8,4,426,42,16),btn("previousCard","‹ 卡",8,52,78),btn("nextCard","卡 ›",92,52,78),
    btn("previousAction","‹ 动作",176,52,84),btn("nextAction","动作 ›",266,52,84),btn("remove","删除",356,52,78)]),
  composite(trialId,"RuntimeAiLabCardTrial",1920,1080,[
    text("title","AI Lab · 真实出牌测试",24,18,1050,48,30),btn("returnObservation","返回行为观察",1640,20,256,40),
    graph("configurationBackground",24,82,470,450),graph("observationBackground",1390,82,506,432),
    text("configurationTitle","初始条件 · 独立离线会话",36,90,446,32,22),
    btn("player","玩家",36,130,216),btn("opponent","对手",264,130,218),
    text("momentumLabel","动势",36,180,52,34),input("initialMomentum","50",94,180,68),
    text("seedLabel","种子",174,180,52,34),input("seed","73",232,180,96),
    text("costLabel","准备费",340,180,68,34),input("preparationCost","5",412,180,70),
    list("initialHand",rowId,36,224,446,186,"Column"),
    btn("addHand","加一张",36,420,138),btn("start","按配置开始",186,420,144),btn("reset","同条件重置",342,420,140),
    text("conditions","",36,462,446,66,13,"#ffa6b4ad"),
    text("feedback","",514,86,850,114,19,"#ffffa4ae"),
    text("worldHint","点击正式手牌选择；需要定点时点地面，再确认。F9 隐藏界面。",514,430,850,50,17,"#ffa6b4ad"),
    btn("confirm","确认出牌",716,482,230,40),btn("cancel","取消待确认",970,482,230,40),
    text("observation","",1402,94,482,412,16),list("hand",face,24,536,1872,528,"Row")])
];
function appendReviewed(file, key, identity, items) {
  let source=fs.readFileSync(file,"utf8"); const document=JSON.parse(source), additions=[];
  for(const item of items) {
    const existing=document[key].find(value=>value[identity]===item[identity]);
    if(!existing) additions.push(item);
    else if(JSON.stringify(existing)!==JSON.stringify(item)) {
      const eol=source.includes("\r\n")?"\r\n":"\n";
      const render=value=>JSON.stringify(value,null,2).split("\n").map(line=>"    "+line).join(eol);
      if(!source.includes(render(existing))) throw Error("Controlled trial formatting changed: "+item[identity]);
      source=source.replace(render(existing),render(item));
    }
  }
  if(!additions.length) {fs.writeFileSync(file,source,"utf8");return;}
  const eol=source.includes("\r\n")?"\r\n":"\n";
  const insert=additions.map(item=>JSON.stringify(item,null,2).split("\n").map(line=>"    "+line).join(eol)).join(","+eol);
  // Both controlled files end with their top-level recipe/node array. Preserve every unrelated byte.
  const ending=/\r?\n  \]\r?\n\}\s*$/;
  if(!ending.test(source)) throw Error("Unexpected controlled file layout: "+file);
  source=source.replace(ending,","+eol+insert+eol+"  ]"+eol+"}"+eol);
  fs.writeFileSync(file,source,"utf8");
}
appendReviewed(path.join(lib,"ui-component-recipes.json"),"recipes","componentId",recipes);
const entry=btn("cardTrialEntry","真实出牌测试",744,78,190,40);entry.order=2100;entry.bindingSlot=entry.name;
const prototype=component("cardTrialPrototype",trialId,"",0,0,1920,1080);
prototype.order=2101;prototype.bindingSlot=prototype.name;prototype.visible=false;
appendReviewed(path.join(lib,"screens/character_test_runtime_ai_lab.ui-screen.json"),"nodes","nodeId",[entry,prototype]);
console.log("AI Lab trial recipes and entry authored. Run the reviewed C# contract writer and source-plan check; Apply/Publish remain pending.");
