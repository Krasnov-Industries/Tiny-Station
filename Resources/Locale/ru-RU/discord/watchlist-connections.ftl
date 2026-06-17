discord-watchlist-connection-header =
    { $players ->
        [one] {$players} игрок из списка наблюдения
        *[other] {$players} игроков из списка наблюдения
    } подключился к {$serverName}
discord-watchlist-connection-entry = - { $playerName } с сообщением "{ $message }"{ $expiry ->
    [0] { "" }
    *[other] { " " }(истекает <t:{ $expiry }:R>)
}{ $otherWatchlists ->
    [0] { "" }
    [one] { " " }и ещё { $otherWatchlists } наблюдением
    [few] { " " }и ещё { $otherWatchlists } наблюдения
    *[other] { " " }и ещё { $otherWatchlists } наблюдений
}
