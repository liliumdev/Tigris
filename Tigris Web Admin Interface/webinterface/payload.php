<?

function pay($k, $v)
{
	return base64_encode($k) . ":" . base64_encode($v) . "|";
}

function assoc_array_shuffle($array)
{
    $orig = array_flip($array);
    shuffle($array);
    foreach($array AS $key=>$n)
    {
        $data[$n] = $orig[$n];
    }
    return array_flip($data);
}

$pre_payload = array(
"1" => "citizen_email",
"2" => "citizen_password",
"3" => "#_token",
"4" => ".user_info > a",
"5" => "I HOPE YOU DON'T SEE THIS. IF YOU DO; SHAME ON YOU",
"6" => "#current_health",
"7" => "#side_bar_gold_account_value",
"8" => "http://www.erepublik.com/citizen/validate/name/",
"9" => "http://www.erepublik.com/citizen/validate/email/",
"10" => "http://www.erepublik.com/en/main/register",
"11" => "http://www.erepublik.com/en/military/group-full-member",
"12" => ".product_list li",
"13" => "http://www.erepublik.com/daily_tasks_reward",
"14" => "http://www.erepublik.com/en/military/group-missions",
"15" => ".listing.grounds",
"16" => "http://www.erepublik.com/en/economy/myCompanies",
"17" => ".f_light_blue_big.job_apply",
"18" => "http://www.erepublik.com/en/economy/work",
"19" => "http://www.erepublik.com/en/military/battlefield/",
"20" => "http://www.erepublik.com/en/wars/show/",
"21" => "http://www.erepublik.com/en/military/fight-shooot/",
"22" => "http://www.erepublik.com/en/main/eat?format=json&_token=",
"23" => "http://www.erepublik.com/en/economy/market/",
"24" => ".f_light_blue_big.buyOffer",
"25" => "http://www.erepublik.com/en/economy/postMarketOffer",
"26" => "price",
"27" => "http://www.erepublik.com/en/citizen/change-residence",
"28" => "http://www.erepublik.com/region-list-current-owner/",
"29" => ".big_action.join",
"30" => "http://www.erepublik.com/en/main/group-members"
);

// Let's randomize the payload order and then encode it
$pre_payload = assoc_array_shuffle($pre_payload);
$payload = "";

foreach($pre_payload as $key => $value)
{
	$payload .= pay($key, $value);
}

// remove the last delimiter
$payload = substr($payload, 0, strlen($payload) - 1);
?>