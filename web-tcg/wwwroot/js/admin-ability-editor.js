(function () {
    "use strict";
    const container = document.getElementById("abilitiesContainer");
    if (!container) return;
    const initial = JSON.parse(document.getElementById("abilityRulesData").textContent);
    const geography = JSON.parse(document.getElementById("abilityGeographyData").textContent);
    const types = [[1,"Dégâts"],[2,"Soins"],[3,"Piocher"],[10,"Gagner de l'énergie"],[11,"Perdre de l'énergie"],[12,"Voler de l'énergie"],[20,"Provocation / Blocage"],[21,"Bouclier"],[22,"Camouflage"],[30,"Coup d'état : attaques supplémentaires"],[31,"Multiplier les dégâts"],[32,"Bonus de PV maximum (+PV)"],[33,"Bonus de dégâts des compétences (+attaque)"],[40,"Second souffle : revivre à 50 %"],[50,"Défausser"],[60,"Détruire le lieu adverse"]];
    const triggers = [[1,"Quand la carte est jouée"],[2,"À l'utilisation de la compétence"],[3,"À l'utilisation du talent"],[4,"Passif (états et bonus liés à la présence sur le terrain)"],[5,"À la mort — Dernier souffle"],[6,"Au début de votre tour"],[7,"À la fin de votre tour"],[8,"Quand la carte subit des dégâts"]];
    const targets = [[1,"Cette carte"],[10,"Un personnage allié"],[11,"Votre Leader"],[12,"Un personnage allié ou votre Leader"],[20,"Un personnage adverse"],[21,"Le Leader adverse"],[22,"Un personnage adverse ou son Leader"],[31,"Le lieu adverse"],[40,"Votre joueur"],[41,"Le joueur adverse"],[50,"Tous vos personnages"],[51,"Tous les personnages adverses"],[52,"Tous les personnages des deux joueurs"]];
    const conditions = [[1,"PV de cette carte inférieurs à"],[2,"PV de cette carte supérieurs à"],[10,"PV de la cible inférieurs à"],[11,"PV de la cible supérieurs à"],[20,"Votre énergie au moins égale à"],[30,"L'adversaire possède un personnage"],[31,"L'adversaire possède un lieu"],[40,"Nombre de personnages alliés au moins égal à"],[41,"Nombre de personnages adverses au moins égal à"],[50,"Cette carte provient des régions choisies"],[51,"Cette carte provient des pays choisis"],[52,"La cible provient des régions choisies"],[53,"La cible provient des pays choisis"],[60,"Personnages alliés des régions/pays choisis : au moins"],[61,"Personnages adverses des régions/pays choisis : au moins"],[62,"Personnages des deux joueurs des régions/pays choisis : au moins"]];
    let sequence = 0;
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
        filters.appendChild(node("p","Filtrer les bénéficiaires : plusieurs choix possibles. Aucun choix = toutes les origines. Région et pays se combinent avec ET.","ability-rules-help"));
        geographicChoices(filters,"Régions des bénéficiaires",`${name}.RegionIds`,geography.regions,value.regionIds);
        geographicChoices(filters,"Pays des bénéficiaires",`${name}.CountryIds`,geography.countries,value.countryIds);
        row.appendChild(filters);
        const scale = input(`${name}.ScaleByMatchingCharacters`,"true","checkbox"); scale.checked = !!value.scaleByMatchingCharacters;
        const scaleField = field(row,"Multiplier le bonus par le nombre de personnages correspondants",scale);
        row.appendChild(input(`${name}.ScaleByMatchingCharacters`,"false","hidden"));
        const count = node("div",null,"ability-geography-panel");
        count.appendChild(node("p","Exemple : valeur 20 et deux personnages correspondants = +40. Les Leaders, la main et le cimetière ne sont pas comptés.","ability-rules-help"));
        field(count,"Compter sur le terrain",select(`${name}.CountScope`,[[1,"Votre terrain"],[2,"Terrain adverse"],[3,"Les deux terrains"]],value.countScope ?? 1));
        geographicChoices(count,"Régions des personnages comptés",`${name}.CountRegionIds`,geography.regions,value.countRegionIds);
        geographicChoices(count,"Pays des personnages comptés",`${name}.CountCountryIds`,geography.countries,value.countCountryIds);
        row.appendChild(count);
        row.appendChild(button("Retirer l'action",()=>row.remove())); list.appendChild(row);
        const update = () => {
            const t = Number(effect.value), player = [3,10,11,12,50].includes(t), durable = [20,21,22,30,31,32,33].includes(t);
            filters.hidden = player || t === 60;
            scaleField.hidden = ![32,33].includes(t);
            count.hidden = ![32,33].includes(t) || !scale.checked;
            amount.step = t === 31 ? "0.1" : "1";
            valueField.querySelector("span").textContent = ({1:"Dégâts infligés",2:"PV restaurés",3:"Cartes piochées",10:"Énergie gagnée",11:"Énergie perdue",12:"Énergie volée",30:"Attaques supplémentaires",31:"Multiplicateur (2 = dégâts doublés)",32:"PV maximum ajoutés",33:"Dégâts ajoutés à chaque compétence",50:"Cartes défaussées"})[t] || "Valeur (1 pour cet état)";
            duration.readOnly = !durable; duration.parentElement.hidden = !durable;
            for (const option of target.options) {
                const v = Number(option.value);
                option.disabled = player ? (t === 12 ? v !== 41 : ![40,41].includes(v)) : t === 60 ? v !== 31 : t === 40 ? v !== 1 : [31,40,41].includes(v) || t === 20 && ([11,12,21,22].includes(v) || cardType() === 1 && v === 1);
            }
            for (const option of trigger.options) {
                const v = Number(option.value), a = abilityType(item);
                option.disabled = cardType() === 3 ? v !== 1 : a === 1 ? v !== 2 : a === 2 ? v !== 3 : [2,3].includes(v);
            }
        };
        effect.addEventListener("change",()=> { update(); duration.value = 0; if (target.selectedOptions[0]?.disabled) target.value = Array.from(target.options).find(o=>!o.disabled)?.value; });
        scale.addEventListener("change",update);
        item.addEventListener("ability-type-change",()=>{ trigger.value = defaultTrigger(item); update(); });
        update();
    }
    function addCondition(item,list,value = {}) {
        const prefix = `Abilities[${item.dataset.abilityKey}].Conditions`, key = sequence++;
        const row = node("div",null,"ability-rule-row"); row.appendChild(input(`${prefix}.Index`,key,"hidden"));
        const name = `${prefix}[${key}]`;
        const type = select(`${name}.Type`,conditions,value.type ?? 20);
        field(row,"Condition",type);
        const threshold = field(row,"Seuil",input(`${name}.Value`,value.value ?? 1));
        const regionGroup = geographicChoices(row,"Régions de la condition",`${name}.RegionIds`,geography.regions,value.regionIds);
        const countryGroup = geographicChoices(row,"Pays de la condition",`${name}.CountryIds`,geography.countries,value.countryIds);
        const update = () => { const t = Number(type.value); regionGroup.hidden = ![50,52,60,61,62].includes(t); countryGroup.hidden = ![51,53,60,61,62].includes(t); threshold.hidden = [50,51,52,53].includes(t); };
        type.addEventListener("change",update); update();
        const negate = input(`${name}.Negate`,"true","checkbox"); negate.checked = !!value.negate;
        field(row,"Inverser la condition",negate); row.appendChild(input(`${name}.Negate`,"false","hidden"));
        row.appendChild(button("Retirer la condition",()=>row.remove())); list.appendChild(row);
    }
    function attach(item) {
        if (item.dataset.rulesReady) return; item.dataset.rulesReady = "true";
        const data = initial[Number(item.dataset.abilityKey)] || {};
        if (!initial[Number(item.dataset.abilityKey)] && [3,4].includes(cardType())) item.querySelector('[name$=".Type"]').value = "3";
        const section = node("section",null,"ability-rules"); section.appendChild(node("h3","Actions en jeu"));
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
        section.append(presets,node("h3","Conditions (facultatif)"),conditionList,button("Ajouter une condition",()=>addCondition(item,conditionList)));
        (data.conditions || []).forEach(c=>addCondition(item,conditionList,c)); item.appendChild(section);
        const update = () => {
            cooldown.parentElement.hidden = abilityType(item) !== 2;
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
