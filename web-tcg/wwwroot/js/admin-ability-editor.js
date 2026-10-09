(function () {
    "use strict";
    const container = document.getElementById("abilitiesContainer");
    if (!container) return;
    const initial = JSON.parse(document.getElementById("abilityRulesData").textContent);
    const geography = JSON.parse(document.getElementById("abilityGeographyData").textContent);
    geography.parties ||= [];
    const orientations = [{id:1,name:"Extrême gauche"},{id:2,name:"Gauche"},{id:3,name:"Centre"},{id:4,name:"Droite"},{id:5,name:"Extrême droite"}];
    const types = [[1,"Dégâts"],[2,"Soins"],[3,"Piocher"],[10,"Gagner de l'énergie"],[11,"Perdre de l'énergie"],[12,"Voler de l'énergie"],[20,"Provocation"],[21,"Bouclier"],[22,"Camouflage"],[23,"Prison : aucune action pendant le prochain tour"],[30,"Coup d'état : attaques supplémentaires"],[31,"Multiplier les dégâts"],[32,"Bonus de PV maximum (+PV)"],[33,"Bonus de dégâts des compétences (+attaque)"],[40,"Second souffle : revivre à 50 %"],[50,"Défausser"],[60,"Détruire le lieu adverse"]];
    const triggers = [[1,"Quand la carte est jouée"],[2,"À l'utilisation de la compétence"],[3,"À l'utilisation du talent"],[4,"Passif (états et bonus liés à la présence sur le terrain)"],[5,"À la mort — Dernier souffle"],[6,"Au début de votre tour"],[7,"À la fin de votre tour"],[8,"Quand la carte subit des dégâts"]];
    types.push([61,"KO : envoyer la carte au cimetière"]);
    const targets = [[1,"Cette carte"],[10,"Un personnage allié"],[11,"Votre Leader"],[12,"Un personnage allié ou votre Leader"],[20,"Un personnage adverse"],[21,"Le Leader adverse"],[22,"Un personnage adverse ou son Leader"],[31,"Le lieu adverse"],[40,"Votre joueur"],[41,"Le joueur adverse"],[50,"Tous vos personnages"],[51,"Tous les personnages adverses"],[52,"Tous les personnages des deux joueurs"]];
    const conditions = [[1,"PV de cette carte inférieurs à"],[2,"PV de cette carte supérieurs à"],[10,"PV de la cible inférieurs à"],[11,"PV de la cible supérieurs à"],[20,"Votre énergie au moins égale à"],[30,"L'adversaire possède un personnage"],[31,"L'adversaire possède un lieu"],[40,"Nombre de personnages alliés au moins égal à"],[41,"Nombre de personnages adverses au moins égal à"],[50,"Cette carte provient des régions choisies"],[51,"Cette carte provient des pays choisis"],[52,"La cible provient des régions choisies"],[53,"La cible provient des pays choisis"],[54,"Cette carte appartient aux partis choisis"],[55,"La cible appartient aux partis choisis"],[60,"Personnages alliés des régions/pays/partis choisis : au moins"],[61,"Personnages adverses des régions/pays/partis choisis : au moins"],[62,"Personnages des deux joueurs des régions/pays/partis choisis : au moins"],[70,"Dévoiler des cartes de votre main pour activer"],[71,"Défausser des cartes de votre main pour activer"]];
    let sequence = 0;
    targets.splice(7,0,[30,"Votre lieu"]);
    function node(tag, text, cls) {
        const e = document.createElement(tag); if (text) e.textContent = text; if (cls) e.className = cls; return e;
    }
    function input(name, value, type = "number") {
        const e = node("input"); e.type = type; e.name = name; e.value = value;
        if (type === "number") { e.min = 0; e.max = 100000; e.step = "1"; e.required = true; } return e;
    }
    function select(name, values, current) {
        const e = node("select"); e.name = name;
        values.forEach(([v,text]) => { const option = node("option",text); option.value = v; e.appendChild(option); });
        e.value = current ?? values[0][0]; return e;
    }
    function field(row, text, control) { const label = node("label",null,"ability-rule-field"); label.append(node("span",text), control); row.appendChild(label); return label; }
    function button(text, action) { const e = node("button",text,"ability-rule-button"); e.type = "button"; e.addEventListener("click",action); return e; }
    function geographicChoices(parent,legend,name,choices,selected = []) {
        const group = node("fieldset",null,"ability-geography-choices"); group.appendChild(node("legend",legend));
        if (!choices.length) group.appendChild(node("small","Aucun choix disponible dans le catalogue."));
        const list = node("div",null,"ability-geography-options");
        choices.forEach(choice=>{ const checkbox = input(name,choice.id,"checkbox"); checkbox.checked = selected.includes(choice.id);
            const label = node("label"); label.append(checkbox,node("span",choice.name)); list.appendChild(label); });
        group.appendChild(list); parent.appendChild(group); return group;
    }
    function cardType() { const value = document.getElementById("Type").value; return ({Leader:1,Character:2,PoliticalDecision:3,Location:4})[value] || Number(value); }
    function abilityType(item) { return Number(item.querySelector('[name$=".Type"]').value); }
    function defaultTrigger(item) { return cardType() === 3 ? 1 : abilityType(item) === 1 ? 2 : abilityType(item) === 2 ? 3 : 4; }
    function addEffect(item, list, value = {}) {
        const prefix = `Abilities[${item.dataset.abilityKey}].Effects`, key = sequence++;
        const row = node("div",null,"ability-rule-row"); row.appendChild(input(`${prefix}.Index`,key,"hidden"));
        row.dataset.ruleKind = "action";
        const name = `${prefix}[${key}]`;
        const effect = select(`${name}.Type`,types,value.type ?? (cardType() === 4 ? 10 : abilityType(item) === 3 ? 21 : 1));
        const trigger = select(`${name}.Trigger`,triggers,value.trigger ?? defaultTrigger(item));
        const target = select(`${name}.Target`,targets,value.target ?? (cardType() === 4 ? 40 : abilityType(item) === 3 ? 1 : 22));
        const amount = input(`${name}.Value`,value.value ?? (abilityType(item) === 3 ? 1 : 10));
        const duration = input(`${name}.DurationTurns`,value.durationTurns ?? 0); duration.max = 99;
        field(row,"Action",effect); field(row,"Déclenchement",trigger); field(row,"Cible",target);
        const valueField = field(row,"Valeur",amount); field(row,"Prochains tours du propriétaire (0 = permanent)",duration);
        row.appendChild(input(`${name}.ParametersJson`,value.parametersJson ?? "{}","hidden"));
        const filters = node("div",null,"ability-geography-panel");
        filters.appendChild(node("p","Filtrer les bénéficiaires : plusieurs choix possibles. Sans sélection : aucune restriction. Région, pays, parti et orientation politique se combinent avec ET ; les choix d'une même catégorie avec OU.","ability-rules-help"));
        geographicChoices(filters,"Régions des bénéficiaires",`${name}.RegionIds`,geography.regions,value.regionIds);
        geographicChoices(filters,"Pays des bénéficiaires",`${name}.CountryIds`,geography.countries,value.countryIds);
        geographicChoices(filters,"Partis des bénéficiaires",`${name}.PartyIds`,geography.parties,value.partyIds);
        geographicChoices(filters,"Orientations politiques des bénéficiaires",`${name}.OrientationIds`,orientations,value.orientationIds);
        row.appendChild(filters);
        const scale = input(`${name}.ScaleByMatchingCharacters`,"true","checkbox"); scale.checked = !!value.scaleByMatchingCharacters;
        const scaleField = field(row,"Multiplier le bonus par le nombre de personnages correspondants",scale);
        row.appendChild(input(`${name}.ScaleByMatchingCharacters`,"false","hidden"));
        const count = node("div",null,"ability-geography-panel");
        count.appendChild(node("p","Exemple : valeur 20 et deux personnages correspondants = +40. Les Leaders, la main et le cimetière ne sont pas comptés.","ability-rules-help"));
        field(count,"Compter sur le terrain",select(`${name}.CountScope`,[[1,"Votre terrain"],[2,"Terrain adverse"],[3,"Les deux terrains"]],value.countScope ?? 1));
        geographicChoices(count,"Régions des personnages comptés",`${name}.CountRegionIds`,geography.regions,value.countRegionIds);
        geographicChoices(count,"Pays des personnages comptés",`${name}.CountCountryIds`,geography.countries,value.countCountryIds);
        geographicChoices(count,"Partis des personnages comptés",`${name}.CountPartyIds`,geography.parties,value.countPartyIds);
        geographicChoices(count,"Orientations politiques des personnages comptés",`${name}.CountOrientationIds`,orientations,value.countOrientationIds);
        row.appendChild(count);
        row.appendChild(button("Retirer l'action",()=>row.remove())); list.appendChild(row);
        const update = () => {
            const t = Number(effect.value), player = [3,10,11,12,50].includes(t), durable = [20,21,22,23,30,31,32,33].includes(t);
            filters.hidden = player || t === 60;
            scaleField.hidden = ![32,33].includes(t);
            count.hidden = ![32,33].includes(t) || !scale.checked;
            amount.min = [32,33].includes(t) ? -100000 : 0;
            amount.step = t === 31 ? "0.1" : "1";
            valueField.querySelector("span").textContent = ({1:"Dégâts infligés",2:"PV restaurés",3:"Cartes piochées",10:"Énergie gagnée",11:"Énergie perdue",12:"Énergie volée",30:"Attaques supplémentaires",31:"Multiplicateur (2 = dégâts doublés)",32:"PV maximum (+ bonus / − malus)",33:"Dégâts des compétences (+ bonus / − malus)",50:"Cartes défaussées"})[t] || "Valeur (1 pour cet état)";
            duration.readOnly = !durable || t === 23; duration.parentElement.hidden = !durable;
            duration.parentElement.querySelector("span").textContent = t === 23 ? "Prison : un seul prochain tour" : "Prochains tours du propriétaire (0 = permanent)";
            if (t === 23) { duration.value = 1; amount.value = 1; }
            valueField.hidden = t === 23 || t === 61;
            if (t === 61) amount.value = 0;
            for (const option of target.options) {
                const v = Number(option.value);
                option.disabled = player ? (t === 12 ? v !== 41 : ![40,41].includes(v)) : t === 60 ? v !== 31 : t === 61 ? ![1,10,20,30,31,50,51,52].includes(v) || v === 1 && ![2,4].includes(cardType()) : t === 40 ? v !== 1 : [30,31,40,41].includes(v) || t === 20 && ([11,12,21,22].includes(v) || cardType() === 1 && v === 1);
                if ([4,5,6,7].includes(Number(trigger.value)) && [10,12,20,22].includes(v) || Number(trigger.value) === 8 && [10,12].includes(v)) option.disabled = true;
            }
            for (const option of trigger.options) {
                const v = Number(option.value), a = abilityType(item);
                option.disabled = (v === 4 && ![20,21,22,30,31,32,33].includes(t)) || (cardType() === 3 ? v !== 1 : a === 1 ? v !== 2 : a === 2 ? v !== 3 : [2,3].includes(v));
            }
        };
        effect.addEventListener("change",()=> { duration.value = 0; update(); if (target.selectedOptions[0]?.disabled) target.value = Array.from(target.options).find(o=>!o.disabled)?.value; });
        scale.addEventListener("change",update);
        trigger.addEventListener("change",update);
        item.addEventListener("ability-type-change",()=>{ trigger.value = defaultTrigger(item); update(); });
        update();
    }
    function addCondition(item,list,value = {}) {
        const prefix = `Abilities[${item.dataset.abilityKey}].Conditions`, key = sequence++;
        const row = node("div",null,"ability-rule-row"); row.appendChild(input(`${prefix}.Index`,key,"hidden"));
        row.dataset.ruleKind = "condition";
        const name = `${prefix}[${key}]`;
        const type = select(`${name}.Type`,conditions,value.type ?? 20);
        [[56,"Cette carte appartient aux orientations politiques choisies"],[57,"La cible appartient aux orientations politiques choisies"],[58,"Votre Leader appartient aux orientations politiques choisies"],[59,"Le Leader adverse appartient aux orientations politiques choisies"],[80,"Un Leader portant ce nom est sur le terrain"],[81,"Un Personnage ou un Lieu portant ce nom est sur le terrain"]].forEach(([id,label]) => {
            const option = node("option",label); option.value=id; type.appendChild(option);
        });
        type.value=value.type ?? 20;
        field(row,"Condition",type);
        const threshold = field(row,"Seuil",input(`${name}.Value`,value.value ?? 1));
        const cardName = input(`${name}.CardName`,value.cardName ?? "","text"); cardName.maxLength=150; cardName.placeholder="Nom exact de la carte";
        const nameField=field(row,"Nom de la carte requise",cardName);
        const scopeField=field(row,"Camp de la carte requise",select(`${name}.Scope`,[[1,"Votre camp"],[2,"Camp adverse"],[3,"Les deux camps"]],value.scope ?? 1));
        const regionGroup = geographicChoices(row,"Régions de la condition",`${name}.RegionIds`,geography.regions,value.regionIds);
        const countryGroup = geographicChoices(row,"Pays de la condition",`${name}.CountryIds`,geography.countries,value.countryIds);
        const partyGroup = geographicChoices(row,"Partis de la condition",`${name}.PartyIds`,geography.parties,value.partyIds);
        const orientationGroup = geographicChoices(row,"Orientations politiques de la condition",`${name}.OrientationIds`,orientations,value.orientationIds);
        const typesGroup = geographicChoices(row,"Types de cartes autorisées",`${name}.CardTypes`,[{id:2,name:"Personnage"},{id:3,name:"Décision politique"},{id:4,name:"Lieu"},{id:5,name:"SECRET"}],value.cardTypes);
        const update = () => { const t = Number(type.value), payment = [70,71].includes(t); typesGroup.hidden = !payment; threshold.querySelector("span").textContent = payment ? "Nombre exact de cartes à choisir" : "Seuil"; threshold.querySelector("input").min=payment ? 1 : 0; threshold.querySelector("input").max=payment ? 40 : 100000; partyGroup.hidden = ![54,55,60,61,62,70,71].includes(t); regionGroup.hidden = ![50,52,60,61,62,70,71].includes(t); countryGroup.hidden = ![51,53,60,61,62,70,71].includes(t); threshold.hidden = [50,51,52,53,54,55].includes(t); };
        type.addEventListener("change",update); update();
        const updateName = () => { const t=Number(type.value), named=[80,81].includes(t); nameField.hidden=scopeField.hidden=!named; cardName.required=named; orientationGroup.hidden=![56,57,58,59,60,61,62,70,71].includes(t); if(named || [56,57,58,59].includes(t)) threshold.hidden=true; };
        type.addEventListener("change",updateName); updateName();
        const negate = input(`${name}.Negate`,"true","checkbox"); negate.checked = !!value.negate;
        const inversion=field(row,"Inverser la condition",negate); const updateInversion=()=>{ inversion.hidden=[70,71].includes(Number(type.value)); if(inversion.hidden) negate.checked=false; }; type.addEventListener("change",updateInversion); updateInversion(); row.appendChild(input(`${name}.Negate`,"false","hidden"));
        row.appendChild(button("Retirer la condition",()=>row.remove())); list.appendChild(row);
    }
    function attach(item) {
        if (item.dataset.rulesReady) return; item.dataset.rulesReady = "true";
        const data = initial[Number(item.dataset.abilityKey)] || {};
        if (!initial[Number(item.dataset.abilityKey)] && [3,4].includes(cardType())) item.querySelector('[name$=".Type"]').value = "3";
        const section = node("section",null,"ability-rules"); section.appendChild(node("h3","1. Définir ce que fait la capacité"));
        const help = node("p","La description est le texte de la carte. Configurez ici ce qu'elle fait réellement. Les actions sont exécutées dans l'ordre ; toutes les conditions doivent être remplies.","ability-rules-help"); section.appendChild(help);
        const cooldown = input(`Abilities[${item.dataset.abilityKey}].CooldownTurns`,data.cooldownTurns ?? 0); cooldown.max = 99;
        field(section,"Recharge du talent (tours à attendre, y compris à la pose)",cooldown);
        const list = node("div",null,"ability-effects-list"), conditionList = node("div"); section.appendChild(list);
        (data.effects || []).forEach(e=>addEffect(item,list,e));
        section.appendChild(button("Ajouter une action",()=>addEffect(item,list)));
        const presets = node("div",null,"ability-rule-presets");
        [["Bouclier",21],["Camouflage",22],["Provocation",20],["Attaque ×2",31],["Coup d'état",30],["Second souffle",40]].forEach(([label,type])=>{
            const b = button(label,()=>addEffect(item,list,{type,target:cardType() === 4 ? 50 : 1,value:type === 31 ? 2 : 1,trigger:type === 40 ? 5 : defaultTrigger(item)}));
            b.dataset.effectType = type; presets.appendChild(b);
        });
        section.append(presets,node("h3","2. Conditions et coûts supplémentaires"),
            node("p","Toutes les conditions doivent être remplies (ET). « Cette carte » désigne la source ; « la cible » désigne chaque bénéficiaire. Pour donner +20 PV aux personnages d’Europe, choisissez un groupe comme cible et Europe dans les régions des bénéficiaires de l’action.","ability-rules-help"),
            conditionList,button("Ajouter une condition",()=>addCondition(item,conditionList)));
        (data.conditions || []).forEach(c=>addCondition(item,conditionList,c)); item.appendChild(section);
        const summary = node("aside",null,"ability-rule-summary");
        summary.appendChild(node("h3","3. Lecture de la capacité"));
        const summaryCost = node("p"), summaryConditions = node("ul"), summaryActions = node("ol");
        summary.append(summaryCost, summaryConditions, summaryActions); section.appendChild(summary);
        const selected = (row,suffix) => row.querySelector(`[name$=".${suffix}"]`);
        const optionText = control => control?.selectedOptions?.[0]?.textContent || "";
        const filtersText = row => [...row.querySelectorAll("fieldset")].filter(group=>!group.hidden)
            .map(group=>{ const names=[...group.querySelectorAll('input[type="checkbox"]:checked')].map(box=>box.parentElement.textContent.trim());
                return names.length ? `${group.querySelector('legend').textContent} : ${names.join(' ou ')}` : ""; }).filter(Boolean).join(" ; ");
        const summarize = () => {
            const kind=abilityType(item), cost=Number(item.querySelector('[name$=".UsageCost"]')?.value || 0);
            summaryCost.textContent = kind===1 ? `Compétence · ${cost} énergie · épuise la carte après utilisation.`
                : kind===2 ? `Talent · ${cost} énergie · recharge de ${cooldown.value} tour(s), à la pose et après utilisation · n’épuise pas la carte.`
                : "Effet · sans coût d’activation · automatique au déclenchement choisi.";
            summaryConditions.replaceChildren(); summaryActions.replaceChildren();
            [...conditionList.children].forEach(row=>{
                const condition=selected(row,"Type"), value=selected(row,"Value"), inverted=selected(row,"Negate")?.checked;
                const ids=[50,51,52,53,54,55,56,57,58,59,80,81];
                const named=[80,81].includes(Number(condition.value));
                const extra=named ? ` « ${selected(row,"CardName").value || "nom à renseigner"} » · ${optionText(selected(row,"Scope"))}` : "";
                summaryConditions.appendChild(node("li",`${inverted ? "Ne doit PAS remplir : " : "Requis : "}${optionText(condition)}${extra}${ids.includes(Number(condition.value)) ? "" : ` ${value.value}`}${!named && filtersText(row) ? ` · ${filtersText(row)}` : ""}`));
            });
            [...list.children].forEach(row=>{
                const type=selected(row,"Type"), trigger=selected(row,"Trigger"), target=selected(row,"Target"), value=selected(row,"Value"), duration=selected(row,"DurationTurns");
                const scaled=selected(row,"ScaleByMatchingCharacters")?.checked;
                summaryActions.appendChild(node("li",`${optionText(trigger)} : ${optionText(type)} (${value.value}${scaled ? " par personnage correspondant" : ""}) → ${optionText(target)}${Number(duration.value)>0 ? ` · pendant ${duration.value} prochain(s) tour(s) du bénéficiaire` : [20,21,22,30,31,32,33].includes(Number(type.value)) ? Number(trigger.value)===4 ? " · tant que la source reste sur le terrain" : " · permanent" : ""}${filtersText(row) ? ` · ${filtersText(row)}` : ""}`));
            });
            if (!list.children.length) summaryActions.appendChild(node("li","Aucune action configurée : la description seule ne produit aucun effet en jeu."));
            if ([...conditionList.children].some(row=>[70,71].includes(Number(selected(row,"Type")?.value))) && [...list.children].some(row=>![1,2,3].includes(Number(selected(row,"Trigger")?.value))))
                summaryConditions.appendChild(node("li","À corriger : dévoiler/défausser nécessite une activation de compétence/talent ou une pose. Un effet automatique ne peut pas demander ce paiement."));
        };
        section.addEventListener("input",summarize); section.addEventListener("change",summarize);
        const ruleObserver = new MutationObserver(summarize);
        ruleObserver.observe(list,{childList:true}); ruleObserver.observe(conditionList,{childList:true});
        item.querySelector('[name$=".UsageCost"]')?.addEventListener("input",summarize);
        const update = () => {
            const cost = item.querySelector('[name$=".UsageCost"]');
            if (cost) {
                const passive = abilityType(item) === 3;
                cost.closest('.create-field').hidden = passive;
                cost.readOnly = passive;
                if (passive) cost.value = 0;
            }
            cooldown.parentElement.hidden = abilityType(item) !== 2;
            summarize();
            presets.querySelectorAll("button").forEach(b=>{ const t = Number(b.dataset.effectType); b.disabled = t === 40 && (abilityType(item) !== 3 || cardType() !== 2) || t === 20 && cardType() === 1 || cardType() === 4 && t !== 31;
                b.title = b.disabled ? "Choisissez un effet passif de personnage pour Second souffle ; le Leader ne bloque pas et les lieux ne donnent pas d'états." : ""; });
            help.textContent = cardType() === 3 ? "Décision : choisissez le type Effet, avec un déclenchement à la pose, puis cimetière. Dégâts, soins et ressources sont instantanés ; les états et bonus d'attaque peuvent durer plusieurs tours." : cardType() === 4 ? "Lieu : choisissez le type Effet et des bonus ou malus automatiques. Aucun blocage, bouclier, camouflage, attaque supplémentaire ou résurrection de personnages." : "Compétence : attaque puis épuisement. Talent : recharge initiale et après utilisation, sans épuiser la carte. Une carte déjà épuisée ne peut effectuer aucune action. Effet : automatique, jamais activé manuellement.";
        };
        item.querySelector('[name$=".Type"]').addEventListener("change",()=>{ update(); item.dispatchEvent(new Event("ability-type-change")); });
        document.getElementById("Type").addEventListener("change",()=>{ update(); item.dispatchEvent(new Event("ability-type-change")); }); update();
    }
    container.querySelectorAll(".ability-item").forEach(attach);
    new MutationObserver(()=>container.querySelectorAll(".ability-item").forEach(attach)).observe(container,{childList:true});
})();
