-- phpMyAdmin SQL Dump
-- version 3.5.2.2
-- http://www.phpmyadmin.net
--
-- Host: 127.0.0.1
-- Generation Time: Feb 12, 2013 at 05:03 AM
-- Server version: 5.5.27
-- PHP Version: 5.4.7

SET SQL_MODE="NO_AUTO_VALUE_ON_ZERO";
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;

--
-- Database: `concierge`
--

-- --------------------------------------------------------

--
-- Table structure for table `activity`
--

CREATE TABLE IF NOT EXISTS `activity` (
  `specialty_id` int(11) NOT NULL,
  `doctor_id` int(11) NOT NULL,
  `date` date NOT NULL,
  `patient_id` int(11) NOT NULL,
  `name` int(50) NOT NULL,
  `document_id` int(11) NOT NULL,
  UNIQUE KEY `specialty_id` (`specialty_id`,`doctor_id`),
  KEY `doctor_id` (`doctor_id`),
  KEY `patient_id` (`patient_id`),
  KEY `file_id` (`document_id`),
  KEY `document_id` (`document_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- --------------------------------------------------------

--
-- Table structure for table `detail_group`
--

CREATE TABLE IF NOT EXISTS `detail_group` (
  `id` int(11) NOT NULL,
  `name` varchar(50) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- --------------------------------------------------------

--
-- Table structure for table `detail_item`
--

CREATE TABLE IF NOT EXISTS `detail_item` (
  `detail_group_id` int(11) NOT NULL,
  `patient_id` int(11) NOT NULL,
  `weight` int(11) NOT NULL,
  `text` varchar(50) NOT NULL,
  KEY `detail_group_id` (`detail_group_id`,`patient_id`),
  KEY `patient_id` (`patient_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- --------------------------------------------------------

--
-- Table structure for table `directives`
--

CREATE TABLE IF NOT EXISTS `directives` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `text` varchar(50) NOT NULL,
  `patient_id` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `patient_id` (`patient_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `doctor`
--

CREATE TABLE IF NOT EXISTS `doctor` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `address1` varchar(50) NOT NULL,
  `address2` varchar(50) NOT NULL,
  `address3` varchar(50) NOT NULL,
  `city` varchar(30) NOT NULL,
  `locality1` varchar(30) NOT NULL,
  `locality2` varchar(30) NOT NULL,
  `postal_code` varchar(10) NOT NULL,
  `country` varchar(20) NOT NULL,
  `telephone` varchar(20) NOT NULL,
  `fax` varchar(20) NOT NULL,
  `email` varchar(40) NOT NULL,
  `contact_person` varchar(100) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `document`
--

CREATE TABLE IF NOT EXISTS `document` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `checksum` varchar(1024) NOT NULL,
  `path` varchar(260) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `document_segment`
--

CREATE TABLE IF NOT EXISTS `document_segment` (
  `document_id` int(11) NOT NULL,
  `data` blob NOT NULL,
  `position` int(11) NOT NULL AUTO_INCREMENT,
  PRIMARY KEY (`position`),
  KEY `document_id` (`document_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `patient`
--

CREATE TABLE IF NOT EXISTS `patient` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `first` varchar(50) NOT NULL,
  `last` varchar(50) NOT NULL,
  `dob` date NOT NULL,
  `gender` char(1) NOT NULL,
  `emergency_contact` text NOT NULL,
  PRIMARY KEY (`id`),
  KEY `nameindex` (`last`,`first`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `release_request`
--

CREATE TABLE IF NOT EXISTS `release_request` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `patient_id` int(11) NOT NULL,
  `document_id` int(11) NOT NULL,
  `sent_date` date NOT NULL,
  `state` varchar(10) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `patient_id` (`patient_id`,`document_id`),
  KEY `document_id` (`document_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `release_response`
--

CREATE TABLE IF NOT EXISTS `release_response` (
  `request_id` int(11) NOT NULL,
  `received_date` date NOT NULL,
  `scanned_date` date NOT NULL,
  PRIMARY KEY (`request_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- --------------------------------------------------------

--
-- Table structure for table `specialty`
--

CREATE TABLE IF NOT EXISTS `specialty` (
  `id` int(10) NOT NULL AUTO_INCREMENT,
  `specialty_name` varchar(50) NOT NULL,
  `subspecialty_name` varchar(50) NOT NULL,
  PRIMARY KEY (`specialty_name`,`subspecialty_name`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

--
-- Constraints for dumped tables
--

--
-- Constraints for table `activity`
--
ALTER TABLE `activity`
  ADD CONSTRAINT `activity_ibfk_1` FOREIGN KEY (`doctor_id`) REFERENCES `doctor` (`id`),
  ADD CONSTRAINT `activity_ibfk_2` FOREIGN KEY (`patient_id`) REFERENCES `patient` (`id`),
  ADD CONSTRAINT `activity_ibfk_3` FOREIGN KEY (`specialty_id`) REFERENCES `specialty` (`id`),
  ADD CONSTRAINT `activity_ibfk_4` FOREIGN KEY (`document_id`) REFERENCES `document` (`id`);

--
-- Constraints for table `detail_item`
--
ALTER TABLE `detail_item`
  ADD CONSTRAINT `detail_item_ibfk_1` FOREIGN KEY (`detail_group_id`) REFERENCES `detail_group` (`id`),
  ADD CONSTRAINT `detail_item_ibfk_2` FOREIGN KEY (`patient_id`) REFERENCES `patient` (`id`);

--
-- Constraints for table `directives`
--
ALTER TABLE `directives`
  ADD CONSTRAINT `directives_ibfk_1` FOREIGN KEY (`patient_id`) REFERENCES `patient` (`id`);

--
-- Constraints for table `document_segment`
--
ALTER TABLE `document_segment`
  ADD CONSTRAINT `document_segment_ibfk_1` FOREIGN KEY (`document_id`) REFERENCES `document` (`id`);

--
-- Constraints for table `release_request`
--
ALTER TABLE `release_request`
  ADD CONSTRAINT `release_request_ibfk_1` FOREIGN KEY (`patient_id`) REFERENCES `patient` (`id`),
  ADD CONSTRAINT `release_request_ibfk_2` FOREIGN KEY (`document_id`) REFERENCES `document` (`id`);

--
-- Constraints for table `release_response`
--
ALTER TABLE `release_response`
  ADD CONSTRAINT `release_response_ibfk_1` FOREIGN KEY (`request_id`) REFERENCES `release_request` (`id`);

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
