namespace HostelManagement.Services;

/// <summary>
/// The residency agreement as supplied by the client (new_agreement_balaji_1.pdf), with the blanks replaced by
/// {Fields} and the COVID section removed (client decision). The admin can edit it on the Students screen.
/// A line starting with "# " is the title, "## " a heading; every other line is a paragraph.
/// </summary>
internal static class AgreementDefaults
{
    public const string Text = """
        # SHRI BALAJI HOSTEL RESIDENCY AGREEMENT
        This Agreement is made at Dehradun on date {AgreementDate} between Sh. SAMAR MAKHIJA S/o Sh. Amaranth Makhija, R/o Shri Balaji Hostel, Suddhowala, Dehradun, (U.K.) (hereinafter called the Owner) which representation assigns successors and administrators on one part
        First Party Owner
        AND
        Name {StudentName}
        {RelationPrefix} {ParentName}
        R/o {Address}
        hereinafter called the second party
        And whereas the first party is owner of the Shri Balaji {HostelType} Hostel near B.F.I.T., Sudhowala, Dehradun including room annexed with the premises i. annexed consisting of Room No. {RoomNumber}, one bed room with {BedsInRoom} separate beds belongs to {BedsInRoom} different students with common attached bathroom, along with the furniture & fixtures which will hereafter be referred to as the premises at annexed Shri Balaji {HostelType} Hostel near B.F.I.T. Sudhowala, Dehradun.
        And whereas tenant has been allotted bed no. {BedNumber} in Room no. {RoomNumber} and whereas the tenant has agreed to take the above premises belonging to the owner, for residence only and not for sub letting leasing on the terms and conditions contained for a period of 11 months w.e.f {CheckInDate}
        NOW THEREFORE IT IS AGREED AS FOLLOWS:-
        1. That the second party shall pay to the owner an annual fee of Rs. {AnnualFee} on the first day of joining the hostel (as agreed). Above rent include accommodation, mess charges, electric charges, laundry, water, Wi-Fi.
        That the second party will pay Rs.5000/- as security deposit which will be refunded by the first party to second party without any interest at the end of the contract only.
        2. That the first party has the right to deduct the said security deposit if any damage found in occupied premises by the second party.
        ## Rules & Regulations
        1. All hosteliers must appreciate that they are living in community environment and must share each other problems. All hosteliers must stick to the room/bed allotted and must not change without management permission. However room or bed can be changed by the management.
        2. Student hosteliers will adopt good behaviour to hostel staff, management and other hosteliers.
        3. Keep all your valuable under lock and key. The management will not take any responsibility of any item lost or damaged.
        4. No night stay is permitted outside the hostel (without the written request and telephonic confirmation by parents).
        5. Students must return to the hostel by 8:00 PM, if getting late phone call to the management by parents is must.
        6. Students cannot park their vehicles in the hostel premises.
        7. The hostel management will not take any responsibility of any kind of misconduct done by the student hosteliers anywhere outside the premises.
        8. Hostel is equipped with 10000 litre of normal water supply and 1500 litre of Solar Geyser supply. In winters (October to March) availability of hot water depends on sun light and uses by fellow hosteliers.
        9. 24 hours RO water will be available, appreciate no wastage of drinking water.
        10. No Heater, Immersion rod, iron or any electronic item are not allowed in the hostel. If found it will be confiscated and released only after the payment of Rs500/-
        11. Use of loudspeakers or music systems with high volume in the hostel premises is not permitted.
        12. Hostel Meals are not allowed to be taken inside the rooms.
        13. Kitchen is not allowed for personal use of student/hosteliers in any conditions.
        14. Hosteliers requested to save the electricity & water, keep the hostel neat and clean. For fuse or bulb, Tube lights during your session, hostel management will not be responsible for its replacement
        15. No responsibility of security/safety will be taken by the management for hosteliers outside the premises of the hostel.
        16. Rules and restrictions by the management will be final and binding.
        17. Display of offensive photos/Posters are not allowed inside the room.
        18. Outsiders are not allowed for night stay in the hostel. If they stay Rs800/- will charged per day.
        19. Consumption of any kind of tobacco, hard drinks & alcohol is strictly not allowed inside the hostel premises neither drunk hosteliers are allowed inside the hostel premises. Any body found intoxicated, will have to vacate the hostel and in this case remaining fees and deposit will not be refunded and management has the right to demand/recover remaining number of months (of full year contract) rent from expelled hosteliers.
        20. Hostel management will take no responsibility of any mishap pining or misplace of clothes in laundry service.
        21. Ragging is strictly prohibited.
        22. Management will not provide any medical facility except first aid kit and arrangement of ambulance in case of emergency (between 8:00 pm to 8:00 am). All kinds of medical expenses will be borne by the hosteliers themselves including fees of ambulance
        23. The hostel management reserve the right to report any misconduct of the hosteliers to the institute, concern office or parents for necessary action to be taken.
        24. If hostelier vacates the hostel prior to term of contract, he/she needs to pay remaining amount of fees to the management and security deposit will not be refundable.
        25. Security deposit will be refunded only after 45 days of expiry of contract, after complete inspection of room by plumber, carpenter and others is necessary.
        26. Any kind of damage will be recovered from security deposit at the end of the contract.
        27. An amount of 1000 will be deducted from security deposit for Room maintenance.
        28. There will be no adjustment /refund for food or any kind of service not availed.
        29. Hostel is installed with Inverter and generator, in case of Electricity hosteliers are requested to save electricity as inverter have limited backup and generator is only used during examination.
        30. The above rules are liable to modification from time to time as need arises.
        31. Hostel rules & Regulations are to be followed strictly.
        32. In case of Loss of keys, an amount of 900 will be charged for replacement of per lock and fees of carpenter to install/fix the new lock.
        33. Contract period will be from 1st JAN to 31st Dec irrespective of admission date and no refund or waiver will be given in midterm break of above period.
        34. In case anybody wants to pay the fee in two instalments then 5% additional fee is to be charged with a post dated cheque of second instalment in first week of MAY.
        35. Any late payment fees will be subject to fine of Rs100/- per day for first 7 days and Rs200 for any additional days.
        36. College holidays will be considered as off days in hostel as well (most primarily summer and winter break and Diwali holidays). In case of any student who wants to stay during that period food provided will bear extra nominal cost.
        37. Food menu will be decided by the hostel management and students collectively once after each quarter and no changes will be made henceforth in the food menu. Non veg will not be served in the hostel. Wastage of food is strictly not allowed and management has the right to take action on any wastage of food done by hosteliers.
        38. Self-cooking counter can be used by hosteliers only with prior permission.
        39. In case of multiple complaints from fellow hosteliers and on disciplinary issues, hostel management reserves the right to expel hosteliers from hostel.
        40. All disputes are subject to Dehradun court Jurisdiction.
        """;
}
