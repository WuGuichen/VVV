"use strict";
// Writes Composer authoring sources only. FairyGUI helper owns XML and published artifacts.
const fs=require("fs"),path=require("path");
const root=path.resolve(__dirname,"../../.."), lib=path.join(root,"ArtSource/UIKits/MxUiFoundation");
const file=path.join(lib,"ui-component-recipes.json"), original=fs.readFileSync(file,"utf8"), catalog=JSON.parse(original);
const prefix="ui.component.tactical.preparation.", button=prefix+"button", row=prefix+"choice", face="ui.component.tactical.player_card_face", item=prefix+"card";
const node=(name,type,x,y,width,height,extra={})=>({nodeId:name,name,type,order:0,bindingSlot:"",parentContainerId:"",visible:true,layout:{x,y,width,height,horizontalAnchor:"Left",verticalAnchor:"Top"},graph:null,text:null,image:null,component:null,list:null,...extra});
const graph=(n,x,y,w,h,color,radius=0)=>node(n,"Graph",x,y,w,h,{graph:{fillColor:color,cornerRadius:radius}});
const text=(n,value,x,y,w,h,size=17,color="#ffe1dfd3",font="MxUI Sans SC")=>node(n,"Text",x,y,w,h,{text:{value,font,fontSize:size,color,align:"left",bold:false,singleLine:false,input:false,restrict:"",maxLength:0}});
const image=(n,id,x,y,w,h)=>node(n,"Image",x,y,w,h,{image:{resourceId:id,tint:"#ffffffff"}});
const component=(n,id,title,x,y,w,h)=>node(n,"ComponentInstance",x,y,w,h,{component:{componentRecipeId:id,title,iconResourceId:""}});
const btn=(n,title,x,y,w,h=40)=>component(n,button,title,x,y,w,h);
const list=(n,id,x,y,w,h)=>node(n,"List",x,y,w,h,{list:{itemComponentRecipeId:id,layout:"Column",lineGap:8,columnGap:12,selectionMode:"None",overflow:"Scroll",virtualized:true}});
const composite=(id,name,w,h,nodes)=>({componentId:prefix+id,displayName:name,componentType:"Composite",fairyGuiComponentName:name,defaultSize:{width:w,height:h},resourceSlots:[],instanceProperties:[],statePresentation:null,template:{nodes:nodes.map((n,i)=>({...n,order:i}))}});
const standard=structuredClone(catalog.recipes.find(r=>r.componentId==="ui.component.button.card_ui_preview"));
standard.componentId=button;standard.displayName="整备标准按钮";standard.fairyGuiComponentName="TacticalPreparationButton";standard.defaultSize={width:180,height:40};
standard.resourceSlots=[{slotId:"base",resourceId:"ui.button.menu.standard.normal"},{slotId:"over",resourceId:"ui.button.menu.standard.hover"},{slotId:"down",resourceId:"ui.button.menu.standard.pressed"}];
standard.statePresentation.titleFontSize=17;standard.statePresentation.titleColor="#e1dfd3";
const choice=structuredClone(standard);choice.componentId=row;choice.displayName="整备候选项";choice.fairyGuiComponentName="TacticalPreparationChoice";choice.defaultSize={width:300,height:56};
const recipes=[standard,choice,
 composite("card","TacticalPreparationCard",394,594,[component("face",face,"",8,8,378,528),btn("action","选择动作",8,546,330),btn("remove","×",346,546,40),btn("add","添加一张",8,546,378)]),
 composite("entity_header","TacticalPreparationEntityHeader",650,166,[
  text("name","",0,0,480,38,27,"#ffe1dfd3","MxUI Serif SC"),btn("deselect","取消选择",520,0,130,36),
  btn("team","阵营 A",0,48,126),btn("control","控制：AI",138,48,156),
  text("pose","",0,97,650,24,15,"#ffa6b4ad"),btn("rotateLeft","↶ 15°",0,126,90,36),btn("rotateRight","15° ↷",100,126,90,36),btn("addCard","添加行为卡",470,126,180,36)]),
 composite("drawer_header","TacticalPreparationDrawerHeader",320,72,[text("title","角色库",0,0,242,36,25,"#ffe1dfd3","MxUI Serif SC"),btn("collapse","收起",246,0,74,36),text("hint","拖到战场放置，可重复添加",0,44,320,26,15,"#ffa6b4ad")]),
 composite("top_bar","TacticalPreparationTopBar",1180,116,[graph("background",0,0,1180,116,"#f5101c1b",8),
  text("title","战场整备",20,12,200,30,24,"#ffe1dfd3","MxUI Serif SC"),text("status","",228,15,700,28,15,"#ffa6b4ad"),
  btn("preset","工作配置",20,60,340),btn("saveAs","另存为",374,60,150),btn("update","更新预设",538,60,150),btn("start","开始战斗",884,60,176),btn("collapse","收起",1072,60,88),btn("refreshCatalog","刷新卡库",1000,12,160,34)]),
 composite("popup","TacticalPreparationPopup",520,568,[graph("background",0,0,520,568,"#fc101c1b",8),
  text("title","",24,18,400,36,25,"#ffe1dfd3","MxUI Serif SC"),btn("close","×",456,18,40,36),
  text("context","",24,62,472,52,16,"#ffa6b4ad"),
  {...text("input","",24,122,472,44,19),text:{...text("input","",0,0,0,0).text,input:true,singleLine:true,maxLength:80}},
  text("issue","",24,432,472,58,16,"#ffffa4ae"),btn("confirm","确认",24,504,220,40),btn("cancel","取消",256,504,240,40)]),
 composite("toast","TacticalPreparationToast",760,68,[graph("background",0,0,760,68,"#f54d2a27",6)])];
let updated=original;
for(const recipe of recipes){const old=catalog.recipes.find(r=>r.componentId===recipe.componentId);const serialized=JSON.stringify(recipe,null,2);if(old){const oldText=JSON.stringify(old,null,2);if(!updated.includes(oldText))throw Error("Recipe formatting changed: "+recipe.componentId);updated=updated.replace(oldText,serialized);}else{const marker='"recipes": [';updated=updated.replace(marker,marker+"\n"+serialized+",");}}
fs.writeFileSync(file,updated);
const nodes=[graph("background",0,0,1920,1080,"#00000000"),graph("deckPanel",1176,16,724,1048,"#f5101c1b",8),graph("libraryPanel",16,16,350,1048,"#f5101c1b",8),
 component("entityHeader",prefix+"entity_header","",1198,36,680,166),list("deckList",item,1198,220,680,800),
 component("drawerHeader",prefix+"drawer_header","",36,36,310,72),text("search","",36,116,310,44,18),list("libraryList",item,52,180,278,850),list("characterList",row,36,130,310,900),
 btn("leftHandle","角色库",0,466,102,48),component("topBar",prefix+"top_bar","",370,16,1180,116),btn("topHandle","预设与开战",825,0,270,40),
 text("emptyHint","",1208,250,640,80,19,"#ffa6b4ad"),text("libraryEmpty","",36,220,310,100,18,"#ffa6b4ad"),
 graph("popupDismiss",0,0,1920,1080,"#59000000"),component("popupPanel",prefix+"popup","",700,256,520,568),list("popupList",row,724,430,472,250),
 component("toastBackground",prefix+"toast","",580,988,760,68),text("toast","",602,998,716,48,17,"#ffffc2b7")];
const parameterRecipes=["approach_action_details","basic_movement_details","retreat_details","orbit_details","keep_distance_details","facing_step_details"];
const parameterList=list("parameterWindows","ui.component.tactical.retreat_details",0,0,346,448);
parameterList.visible=false;parameterList.list.virtualized=false;parameterList.list.lineGap=12;nodes.push(parameterList);
for(const id of parameterRecipes){const recipe=catalog.recipes.find(r=>r.componentId==="ui.component.tactical."+id);
 const prototype=component("parameterPrototype_"+id,recipe.componentId,"",0,0,recipe.defaultSize.width,recipe.defaultSize.height);
 prototype.visible=false;nodes.push(prototype);}
nodes.forEach((n,i)=>{n.order=i;n.bindingSlot=n.name.startsWith("parameterPrototype_")?"":n.name;if(n.name==="search")n.text={...n.text,input:true,singleLine:true,maxLength:80};});
const screen={schemaVersion:"mx.uiScreenBlueprint.v3",screenId:"ui.screen.character_test.tactical_deck",displayName:"CharacterTest 战场整备",libraryId:"MxUiFoundation",designResolution:{width:1920,height:1080},target:{packageId:"MxCharacterTestTacticalDeckUi",componentName:"TacticalDeckPanel",packageSourcePath:"FGUIProject/assets/MxCharacterTestTacticalDeckUi"},containers:[],nodes};
fs.writeFileSync(path.join(lib,"screens/character_test_tactical_deck.ui-screen.json"),JSON.stringify(screen,null,2)+"\n");
console.log("Configured "+recipes.length+" controlled recipes and "+nodes.length+" screen nodes; Apply/Publish not yet run.");
