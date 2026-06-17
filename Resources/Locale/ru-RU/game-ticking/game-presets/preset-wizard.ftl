## Survivor

roles-antag-survivor-name = Выживший
# It's a Halo reference
roles-antag-survivor-objective = Текущая задача: Выжить
survivor-role-greeting =
    Вы — Выживший. Прежде всего вам нужно живым вернуться в Центральное Командование.
    Соберите столько огневой мощи, сколько нужно, чтобы гарантировать своё выживание.
    Не доверяйте никому.
survivor-round-end-dead-count =
    { $deadCount ->
        [one] [color=red]{ $deadCount }[/color] выживший умер.
       *[other] [color=red]{ $deadCount }[/color] выживших умерло.
    }
survivor-round-end-alive-count =
    { $aliveCount ->
        [one] [color=yellow]{ $aliveCount }[/color] выживший остался на станции.
       *[other] [color=yellow]{ $aliveCount }[/color] выживших осталось на станции.
    }
survivor-round-end-alive-on-shuttle-count =
    { $aliveCount ->
        [one] [color=green]{ $aliveCount }[/color] выживший выбрался живым.
       *[other] [color=green]{ $aliveCount }[/color] выживших выбралось живыми.
    }

## Wizard

objective-issuer-swf = [color=turquoise]Федерация космических волшебников[/color]
wizard-title = Волшебник
wizard-description = На станции присутствует волшебник! Никогда не знаешь, что они могут натворить.
roles-antag-wizard-name = Волшебник
roles-antag-wizard-objective = Преподайте им урок, который они никогда не забудут.
wizard-role-greeting =
    Время волшебства, файрбол!
    Между Федерацией Космических Волшебников и NanoTrasen возникло напряжение. Федерация выбрала вас, чтобы навестить станцию и «напомнить им», почему с чародеями шутки плохи.
    Сейте хаос и разрушение! Что именно делать — решать вам, но помните: Космические Волшебники хотят, чтобы вы выбрались живым.
wizard-round-end-name = волшебник

## TODO: Wizard Apprentice (Coming sometime post-wizard release)

