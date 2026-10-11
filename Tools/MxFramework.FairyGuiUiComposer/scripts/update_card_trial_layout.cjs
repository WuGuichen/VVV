// Controlled Composer source migration. Package XML and published assets remain helper-owned.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../..');
const recipePath = path.join(root, 'ArtSource/UIKits/MxUiFoundation/ui-component-recipes.json');
let source = fs.readFileSync(recipePath, 'utf8');
const catalog = JSON.parse(source);
const prefix = 'ui.component.runtime_ai_lab.';
const neutral = prefix + 'card_trial_button', primary = prefix + 'card_trial_primary_button';
const portrait = prefix + 'card_trial_portrait';
const clone = value => JSON.parse(JSON.stringify(value));
function node(name, type, x, y, width, height, data, visible = true) {
  return { nodeId:name, name, type, order:0, bindingSlot:'', parentContainerId:'', visible,
    layout:{x,y,width,height,horizontalAnchor:'Left',verticalAnchor:'Top'},
    graph:null,text:null,image:null,component:null,list:null,...data };
}
function graph(name,x,y,w,h,color,visible=true,corner=8) {return node(name,'Graph',x,y,w,h,{graph:{fillColor:color,cornerRadius:corner}},visible);}
function text(name,value,x,y,w,h,size=24,visible=true,options={}) {
  return node(name,'Text',x,y,w,h,{text:{value,font:name==='title'||name==='cardName'?'MxUI Serif SC':'MxUI Sans SC',fontSize:size,
    color:'#fff0eee4',align:'left',bold:false,singleLine:false,input:false,restrict:'',maxLength:0,verticalAlign:'middle',...options}},visible);
}
function button(name,title,x,y,w,h,visible=true,recipe=neutral) {
  return node(name,'ComponentInstance',x,y,w,h,{component:{componentRecipeId:recipe,title,iconResourceId:''}},visible);
}
function face(name,x,y,visible=true) {return button(name,'',x,y,378,528,visible,'ui.component.tactical.player_card_face');}
function list(name,recipe,x,y,w,h,layout='Column',visible=true) {
  return node(name,'List',x,y,w,h,{list:{itemComponentRecipeId:recipe,layout,lineGap:12,columnGap:12,
    selectionMode:'None',overflow:'Scroll',virtualized:true}},visible);
}
function component(id,name,width,height,nodes) {
  nodes.forEach((n,i)=>{n.order=i;});
  return {componentId:id,displayName:name,componentType:'Composite',fairyGuiComponentName:name,
    defaultSize:{width,height},resourceSlots:[],instanceProperties:[],statePresentation:null,template:{nodes}};
}
function scanEnd(start) {
  let depth=0, quoted=false, escaped=false;
  for(let i=start;i<source.length;i++) {
    const ch=source[i];
    if(quoted){if(escaped)escaped=false;else if(ch==='\\')escaped=true;else if(ch==='"')quoted=false;continue;}
    if(ch==='"'){quoted=true;continue;}
    if(ch==='{'||ch==='[')depth++;
    else if(ch==='}'||ch===']'){if(--depth===0)return i+1;}
  }
  throw new Error('Unclosed authority JSON');
}
function formatted(recipe) {return JSON.stringify(recipe,null,2).replaceAll('\n','\n    ');}
function upsert(recipe) {
  const marker='"componentId": '+JSON.stringify(recipe.componentId);
  const at=source.indexOf(marker);
  if(at>=0){const start=source.lastIndexOf('{',at),end=scanEnd(start);source=source.slice(0,start)+formatted(recipe)+source.slice(end);}
  else {
    const start=source.indexOf('[',source.indexOf('"recipes"'));
    const end=scanEnd(start)-1;
    const before=source.slice(0,end).trimEnd();
    source=before+',\n    '+formatted(recipe)+'\n  '+source.slice(end);
  }
}
for(const [id,name,base] of [[neutral,'RuntimeAiLabCardTrialButton','ui.component.button.compact.neutral'],
  [primary,'RuntimeAiLabCardTrialPrimaryButton','ui.component.button.compact.primary']]) {
  const recipe=clone(catalog.recipes.find(r=>r.componentId===base));
  if(!recipe)throw new Error('Missing standard button: '+base);
  recipe.componentId=id;recipe.displayName=name;recipe.fairyGuiComponentName=name;
  recipe.defaultSize={width:240,height:54};recipe.statePresentation.titleFontSize=24;
  recipe.statePresentation.titleFont='MxUI Sans SC';recipe.statePresentation.titleBold=false;
  upsert(recipe);
}
const portraitRecipe=clone(catalog.recipes.find(r=>r.componentId==='ui.component.button.compact.neutral'));
portraitRecipe.componentId=portrait;portraitRecipe.displayName='角色头像';portraitRecipe.fairyGuiComponentName='RuntimeAiLabCardTrialPortrait';
portraitRecipe.defaultSize={width:144,height:144};portraitRecipe.statePresentation.iconOnly=true;portraitRecipe.statePresentation.iconSize=128;
upsert(portraitRecipe);
upsert(component(prefix+'card_trial_row','RuntimeAiLabCardTrialRow',300,564,[
  face('face',0,0),text('slotLabel','',0,422,300,28,20,true,{align:'center',color:'#ffbec9c1'}),
  button('chooseAction','选择装配动作',0,458,300,48),
  button('replaceCard','换卡',0,520,92,40),button('copyCard','复制',104,520,92,40),button('remove','移除',208,520,92,40)
]));
upsert(component(prefix+'card_trial_choice','RuntimeAiLabCardTrialChoice',900,116,[
  button('select','',0,0,900,116),button('portrait','',12,10,96,96,false,portrait),
  text('name','',22,14,856,42,28),text('detail','',22,62,856,40,22,true,{color:'#ffbec9c1'})
]));
upsert(component(prefix+'card_trial_card_choice','RuntimeAiLabCardTrialCardChoice',280,440,[
  face('face',0,0),button('select','选择这张卡',0,400,280,40)
]));
upsert(component(prefix+'card_trial_hand_card','RuntimeAiLabCardTrialHandCard',220,308,[
  face('face',0,0)
]));
const n=[
  graph('configurationBackground',0,0,1920,1080,'#ff141c1c'),
  graph('headerBackground',0,0,1920,126,'#ff1c2626',true,0),
  graph('footerBackground',0,966,1920,114,'#ff1c2626',true,0),
  text('title','试验配置',48,28,1200,50,40),text('subtitle','参战角色、初始条件与手牌配好后，进入场景试验。',48,82,1400,32,24,true,{color:'#ffbec9c1'}),
  button('returnObservation','返回 AI Lab',1590,36,282,54),
  text('playerRoleLabel','玩家角色 · 点击头像选择',48,170,464,34,24),button('player','',48,212,144,144,true,portrait),
  text('playerName','',212,228,300,100,28),
  text('opponentRoleLabel','对手角色 · 点击头像选择',48,390,464,34,24),button('opponent','',48,432,144,144,true,portrait),
  text('opponentName','',212,448,300,100,28),
  text('initialConditionsTitle','初始条件',48,622,464,40,30),
  text('momentumLabel','初始动势 · 0–100',48,680,464,34,26),graph('momentumInputBackground',48,724,464,56,'#ff263232'),
  text('initialMomentum','50',64,732,432,40,28,true,{singleLine:true,input:true,maxLength:11}),
  text('seedLabel','随机种子',48,818,220,34,24),graph('seedInputBackground',48,858,220,56,'#ff263232'),
  text('seed','73',64,866,188,40,28,true,{singleLine:true,input:true,maxLength:11}),
  text('costLabel','测试准备费用',292,818,220,34,24),graph('preparationCostInputBackground',292,858,220,56,'#ff263232'),
  text('preparationCost','5',308,866,188,40,28,true,{singleLine:true,input:true,maxLength:11}),
  text('configurationTitle','初始手牌',568,180,1000,40,30),button('addHand','添加卡牌',1640,172,232,54),
  list('initialHand',prefix+'card_trial_row',568,246,1304,630,'Row'),
  text('configurationHint','按配置发初始手牌；已配置行动卡生成备用牌，准备卡不补牌。',568,896,1304,54,22,true,{color:'#ffbec9c1'}),
  text('conditions','',48,938,1824,28,21,true,{color:'#ffbdc9c1'}),
  text('feedback','',48,992,1150,62,24,true,{color:'#ffffaaa0'}),
  button('resumeTrial','返回当前试验',1250,996,270,60,false),button('start','开始试验',1534,996,338,60,true,primary),
  graph('observationBackground',24,694,690,42,'#b8141c1c',false),
  text('observation','',40,700,658,30,22,false),
  button('trialMenu','试验菜单',1704,24,192,46,false),
  graph('trialMenuBackground',1656,82,240,244,'#f51c2626',false),
  button('reset','同条件重试',1668,94,216,46,false),button('showConditions','本次条件',1668,150,216,46,false),
  button('editConfiguration','修改配置',1668,206,216,46,false),
  button('leaveTrial','返回 AI Lab',1668,262,216,46,false),
  text('worldHint','',1480,810,416,80,22,false,{color:'#ffe1e7dd'}),
  button('cancel','取消选择',1680,922,216,46,false),button('confirm','确认出牌',1480,984,416,64,false,primary),
  text('observationFeedback','',1480,810,416,100,22,false,{color:'#ffffb5aa'}),
  list('hand',prefix+'card_trial_hand_card',24,748,1432,308,'Row',false),
  graph('conditionsDetailBackground',928,224,944,704,'#ff1c2626',false),
  text('conditionsDetail','',948,242,814,204,24,false),button('closeConditions','关闭',1778,238,78,42,false),
  list('frozenHand',prefix+'card_trial_card_choice',948,464,904,440,'Row',false),
  graph('pickerBackground',0,126,1920,954,'#ff141c1c',false,0),
  text('pickerTitle','选择卡牌',48,152,1824,48,32,false),
  graph('pickerSearchBackground',48,216,900,56,'#ff263232',false),
  text('pickerSearch','',68,224,860,40,26,false,{singleLine:true,input:true,maxLength:100}),
  button('pickerAll','全部',48,294,180,48,false),button('pickerActions','行动卡',240,294,210,48,false),
  button('pickerPreparations','交互准备卡',462,294,240,48,false),button('pickerTools','测试工具卡',714,294,234,48,false),
  list('pickerChoices',prefix+'card_trial_choice',48,366,900,572,'Column',false),
  list('pickerCards',prefix+'card_trial_card_choice',48,366,900,572,'Row',false),
  graph('pickerDetailBackground',992,216,880,722,'#ff1c2626',false),
  text('pickerDetailTitle','',1020,248,824,120,32,false),face('pickerCardPreview',1038,400,false),
  button('pickerPortraitPreview','',1038,416,340,340,false,portrait),text('pickerDetail','',1450,416,394,480,26,false),
  button('pickerCancel','取消',1250,996,270,60,false),button('pickerApply','确认选择',1534,996,338,60,false,primary),
  graph('restartShield',0,0,1920,1080,'#ad081010',false,0),graph('restartBackground',360,350,1200,380,'#ff1c2626',false),
  text('restartTitle','开始新试验？',400,382,1120,56,36,false),
  text('restartDetail','结束当前试验，使用配置页的角色、参数与手牌重新发牌。',400,462,1120,90,26,false),
  button('restartCancel','保留当前',920,634,240,60,false),button('restartConfirm','结束当前并新开',1180,634,340,60,false,primary)
];
upsert(component(prefix+'card_trial','RuntimeAiLabCardTrial',1920,1080,n));
JSON.parse(source);
if(fs.readFileSync(recipePath,'utf8')!==source)fs.writeFileSync(recipePath,source,'utf8');
console.log('CardTrial compact configuration, observation, picker and confirmation recipes updated; publish through UI Composer.');
